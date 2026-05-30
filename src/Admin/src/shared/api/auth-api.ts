import { apiRaw, apiRequest } from '@/shared/api/client'
import type { AuthResponseDto, TwoFactorChallengeDto } from '@/shared/types/api'
import { ApiClientError } from '@/shared/lib/errors'

export interface LoginInput {
  email: string
  password: string
  turnstileToken?: string | null
}

export type LoginResult =
  | { kind: 'authenticated'; data: AuthResponseDto }
  | { kind: 'twoFactor'; data: TwoFactorChallengeDto }

export async function login(input: LoginInput): Promise<LoginResult> {
  const { turnstileToken, ...body } = input
  const { status, envelope } = await apiRaw<AuthResponseDto | TwoFactorChallengeDto>(
    '/auth/login',
    {
      method: 'POST',
      body: JSON.stringify(body),
      turnstileToken,
    },
  )

  if (status === 202 && envelope.success && envelope.data) {
    return { kind: 'twoFactor', data: envelope.data as TwoFactorChallengeDto }
  }

  if (status === 200 && envelope.success && envelope.data) {
    return { kind: 'authenticated', data: envelope.data as AuthResponseDto }
  }

  if (envelope.error) {
    throw new ApiClientError(envelope.error)
  }

  throw new ApiClientError({
    code: 'LoginFailed',
    message: 'Sign-in failed.',
    httpStatus: status,
  })
}

export async function completeTwoFactorLogin(
  pendingLoginId: string,
  code: string,
): Promise<AuthResponseDto> {
  const { status, envelope } = await apiRaw<AuthResponseDto>('/auth/login/2fa', {
    method: 'POST',
    body: JSON.stringify({ pendingLoginId, code }),
  })

  if (status === 200 && envelope.success && envelope.data) {
    return envelope.data
  }

  if (envelope.error) {
    throw new ApiClientError(envelope.error)
  }

  throw new ApiClientError({
    code: 'TwoFactorFailed',
    message: 'Two-factor verification failed.',
    httpStatus: status,
  })
}

export async function logoutRevoke(refreshToken: string): Promise<void> {
  try {
    await apiRequest<void>('/auth/refresh', {
      method: 'POST',
      body: { refreshToken },
      anonymous: true,
    })
  } catch {
    // Best-effort; session cleared locally regardless.
  }
}
