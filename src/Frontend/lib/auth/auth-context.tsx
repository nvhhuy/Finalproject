"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { AuthTokens } from "./tokens";
import { loadTokens, saveTokens } from "./tokens";
import { authApi } from "@/lib/features/auth/auth-api";
import { userApi } from "@/lib/features/users/user-api";

export type AuthUser = {
  userId: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
};

export type LoginResult =
  | { kind: "ok"; user: AuthUser; tokens: AuthTokens }
  | { kind: "2fa_required"; pendingLoginId: string; message?: string | null }
  | { kind: "error"; message: string };

type AuthState = {
  user: AuthUser | null;
  tokens: AuthTokens | null;
  hydrated: boolean;
};

type AuthContextValue = AuthState & {
  isAuthenticated: boolean;
  login(email: string, password: string, turnstileToken?: string | null): Promise<LoginResult>;
  completeTwoFactor(pendingLoginId: string, code: string): Promise<{ user: AuthUser; tokens: AuthTokens }>;
  refresh(): Promise<AuthTokens | null>;
  logout(): void;
  setSession(user: AuthUser, tokens: AuthTokens): void;
  reloadProfile(): Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

function userFromProfile(p: {
  id: string;
  userName: string;
  email: string;
  firstName: string;
  lastName: string;
}): AuthUser {
  return {
    userId: p.id,
    userName: p.userName,
    email: p.email,
    firstName: p.firstName,
    lastName: p.lastName,
  };
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<AuthState>({
    user: null,
    tokens: null,
    hydrated: false,
  });

  const setSession = useCallback((user: AuthUser, tokens: AuthTokens) => {
    saveTokens(tokens);
    setState((s) => ({ ...s, user, tokens }));
  }, []);

  const logout = useCallback(() => {
    saveTokens(null);
    setState({ user: null, tokens: null, hydrated: true });
  }, []);

  const reloadProfile = useCallback(async () => {
    const res = await userApi.getMyProfile();
    if (res.success && res.data) {
      setState((s) => ({ ...s, user: userFromProfile(res.data!), hydrated: true }));
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    async function hydrate() {
      const tokens = loadTokens();
      if (!tokens?.accessToken) {
        if (!cancelled) setState((s) => ({ ...s, hydrated: true }));
        return;
      }
      try {
        const res = await userApi.getMyProfile();
        if (!cancelled) {
          if (res.success && res.data) {
            setState({ user: userFromProfile(res.data), tokens, hydrated: true });
          } else {
            const refreshed = await authApi.refresh(tokens.refreshToken);
            if (refreshed.kind === "ok") {
              setState({ user: refreshed.user, tokens: refreshed.tokens, hydrated: true });
            } else {
              saveTokens(null);
              setState({ user: null, tokens: null, hydrated: true });
            }
          }
        }
      } catch {
        if (!cancelled) setState((s) => ({ ...s, tokens, hydrated: true }));
      }
    }
    void hydrate();
    return () => {
      cancelled = true;
    };
  }, []);

  const refresh = useCallback(async () => {
    const current = loadTokens();
    if (!current?.refreshToken) return null;
    const result = await authApi.refresh(current.refreshToken);
    if (result.kind !== "ok") {
      logout();
      return null;
    }
    setSession(result.user, result.tokens);
    return result.tokens;
  }, [logout, setSession]);

  const login = useCallback(
    async (email: string, password: string, turnstileToken?: string | null): Promise<LoginResult> => {
      const result = await authApi.login({ email, password, turnstileToken });
      if (result.kind === "ok") setSession(result.user, result.tokens);
      return result;
    },
    [setSession],
  );

  const completeTwoFactor = useCallback(
    async (pendingLoginId: string, code: string) => {
      const result = await authApi.completeTwoFactor({ pendingLoginId, code });
      if (result.kind !== "ok") throw new Error(result.message ?? "2FA failed");
      setSession(result.user, result.tokens);
      return { user: result.user, tokens: result.tokens };
    },
    [setSession],
  );

  const value: AuthContextValue = useMemo(
    () => ({
      ...state,
      isAuthenticated: state.hydrated && Boolean(state.tokens?.accessToken),
      login,
      completeTwoFactor,
      refresh,
      logout,
      setSession,
      reloadProfile,
    }),
    [state, login, completeTwoFactor, refresh, logout, setSession, reloadProfile],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
