import { apiRaw, apiRequest } from "@/lib/api/http";
import type { ApiResponse } from "@/lib/api/types";
import { ApiClientError } from "@/lib/errors";
import type { AuthTokens } from "@/lib/auth/tokens";
import type { AuthUser, LoginResult } from "@/lib/auth/auth-context";

export type AuthResponseDto = {
  userId: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
  accessToken: string;
  accessTokenExpiresUtc: string;
  refreshToken: string;
  refreshTokenExpiresUtc: string;
};

export type TwoFactorChallengeDto = {
  pendingLoginId: string;
  message?: string | null;
};

function mapOk(dto: AuthResponseDto): { user: AuthUser; tokens: AuthTokens } {
  const user: AuthUser = {
    userId: dto.userId,
    userName: dto.userName,
    email: dto.email,
    firstName: dto.firstName,
    lastName: dto.lastName,
    emailVerified: true,
  };
  const tokens: AuthTokens = {
    accessToken: dto.accessToken,
    accessTokenExpiresUtc: dto.accessTokenExpiresUtc,
    refreshToken: dto.refreshToken,
    refreshTokenExpiresUtc: dto.refreshTokenExpiresUtc,
  };
  return { user, tokens };
}

function throwFromEnvelope(status: number, envelope: ApiResponse<unknown>): never {
  if (envelope.error) throw new ApiClientError(envelope.error);
  throw new ApiClientError({
    code: "HttpError",
    message: `HTTP ${status}`,
    httpStatus: status,
  });
}

export const authApi = {
  async login(input: { email: string; password: string; turnstileToken?: string | null }): Promise<LoginResult> {
    const { email, password, turnstileToken } = input;
    const { status, envelope } = await apiRaw<AuthResponseDto | TwoFactorChallengeDto>(
      "/api/v1/auth/login",
      { method: "POST", body: JSON.stringify({ email, password }), auth: false, turnstileToken },
    );

    if (status === 202 && envelope.success && envelope.data && "pendingLoginId" in envelope.data) {
      return {
        kind: "2fa_required",
        pendingLoginId: envelope.data.pendingLoginId,
        message: envelope.data.message ?? null,
      };
    }

    if (status === 200 && envelope.success && envelope.data && "accessToken" in envelope.data) {
      const { user, tokens } = mapOk(envelope.data);
      return { kind: "ok", user, tokens };
    }

    if (!envelope.success) {
      if (envelope.error?.code === "EmailNotVerified") {
        return {
          kind: "email_not_verified",
          email,
          message: envelope.error.message ?? "Please verify your email before signing in.",
        };
      }
      return { kind: "error", message: envelope.error?.message ?? "Login failed" };
    }

    throwFromEnvelope(status, envelope);
  },

  async completeTwoFactor(input: { pendingLoginId: string; code: string }) {
    const { status, envelope } = await apiRaw<AuthResponseDto>("/api/v1/auth/login/2fa", {
      method: "POST",
      body: JSON.stringify(input),
      auth: false,
    });
    if (status === 200 && envelope.success && envelope.data) {
      const { user, tokens } = mapOk(envelope.data);
      return { kind: "ok" as const, user, tokens };
    }
    if (envelope.error) return { kind: "error" as const, message: envelope.error.message ?? "2FA failed" };
    return { kind: "error" as const, message: "2FA failed" };
  },

  async refresh(refreshToken: string) {
    const body = await apiRequest<AuthResponseDto>({
      method: "POST",
      path: "/api/v1/auth/refresh",
      body: { refreshToken },
      auth: false,
    });
    if (!body.success || !body.data) return { kind: "error" as const, message: body.error?.message ?? "Refresh failed" };
    const { user, tokens } = mapOk(body.data);
    return { kind: "ok" as const, user, tokens };
  },

  async register(input: {
    email: string;
    userName: string;
    password: string;
    firstName: string;
    lastName: string;
    turnstileToken?: string | null;
  }) {
    const { turnstileToken, ...body } = input;
    return await apiRequest<{ email: string; message: string }>({
      method: "POST",
      path: "/api/v1/auth/register",
      body,
      auth: false,
      idempotent: true,
      turnstileToken,
    });
  },

  async forgotPassword(email: string, turnstileToken?: string | null) {
    return await apiRequest<unknown>({
      method: "POST",
      path: "/api/v1/auth/forgot-password",
      body: { email },
      auth: false,
      turnstileToken,
    });
  },

  async resendVerification(email: string, turnstileToken?: string | null) {
    return await apiRequest<unknown>({
      method: "POST",
      path: "/api/v1/auth/resend-verification",
      body: { email },
      auth: false,
      turnstileToken,
    });
  },

  async resetPassword(input: { tokenId: string; token: string; newPassword: string }) {
    return await apiRequest<unknown>({
      method: "POST",
      path: "/api/v1/auth/reset-password",
      body: input,
      auth: false,
    });
  },

  async verifyEmail(tokenId: string, token: string) {
    return await apiRequest<unknown>({
      method: "POST",
      path: "/api/v1/auth/verify-email",
      body: { tokenId, token },
      auth: false,
    });
  },

  async tenantSsoSignIn(tenantSlug: string, idToken: string) {
    const { status, envelope } = await apiRaw<AuthResponseDto>(`/api/v1/auth/tenant-sso/${encodeURIComponent(tenantSlug)}`, {
      method: "POST",
      body: JSON.stringify({ idToken }),
      auth: false,
    });
    if (status === 200 && envelope.success && envelope.data) {
      const { user, tokens } = mapOk(envelope.data);
      return { kind: "ok" as const, user, tokens };
    }
    if (envelope.error) return { kind: "error" as const, message: envelope.error.message ?? "SSO sign-in failed" };
    return { kind: "error" as const, message: "SSO sign-in failed" };
  },

  async googleSignIn(idToken: string) {
    const { status, envelope } = await apiRaw<AuthResponseDto>("/api/v1/auth/google", {
      method: "POST",
      body: JSON.stringify({ idToken }),
      auth: false,
    });
    if (status === 200 && envelope.success && envelope.data) {
      const { user, tokens } = mapOk(envelope.data);
      return { kind: "ok" as const, user, tokens };
    }
    if (envelope.error) return { kind: "error" as const, message: envelope.error.message ?? "Google sign-in failed" };
    return { kind: "error" as const, message: "Google sign-in failed" };
  },

  async beginTwoFactor() {
    return await apiRequest<{ sharedKey: string; otpAuthUri: string }>({
      method: "POST",
      path: "/api/v1/auth/2fa/begin",
      auth: true,
    });
  },

  async confirmTwoFactor(code: string) {
    return await apiRequest<unknown>({
      method: "POST",
      path: "/api/v1/auth/2fa/confirm",
      auth: true,
      body: { code },
    });
  },
};
