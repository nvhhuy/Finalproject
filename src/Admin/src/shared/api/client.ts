import { API_V1 } from '@/config/env'
import { useAuthStore } from '@/shared/stores/auth-store'
import type { ApiResponse } from '@/shared/types/api'
import { ApiClientError } from '@/shared/lib/errors'
import { newIdempotencyKey } from '@/shared/lib/idempotency'
import type { AuthResponseDto } from '@/shared/types/api'

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'

export interface RequestOptions {
  method?: HttpMethod
  body?: unknown
  /** Send Idempotency-Key header (mutations). */
  idempotent?: boolean
  idempotencyKey?: string
  /** Skip Authorization header (login, refresh). */
  anonymous?: boolean
  signal?: AbortSignal
}

let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = useAuthStore.getState().getRefreshToken()
  if (!refreshToken) {
    return null
  }

  const response = await fetch(`${API_V1}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ refreshToken }),
    credentials: 'same-origin',
  })

  if (!response.ok) {
    useAuthStore.getState().clearSession()
    return null
  }

  const envelope = (await response.json()) as ApiResponse<AuthResponseDto>
  if (!envelope.success || !envelope.data) {
    useAuthStore.getState().clearSession()
    return null
  }

  useAuthStore.getState().setSession(envelope.data)
  return envelope.data.accessToken
}

async function ensureAccessToken(): Promise<string | null> {
  const { accessToken } = useAuthStore.getState()
  if (accessToken) {
    return accessToken
  }
  if (!refreshPromise) {
    refreshPromise = refreshAccessToken().finally(() => {
      refreshPromise = null
    })
  }
  return refreshPromise
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const {
    method = 'GET',
    body,
    idempotent = false,
    idempotencyKey,
    anonymous = false,
    signal,
  } = options

  const headers: Record<string, string> = {
    Accept: 'application/json',
  }

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  if (idempotent || idempotencyKey) {
    headers['Idempotency-Key'] = idempotencyKey ?? newIdempotencyKey()
  }

  if (!anonymous) {
    const token = await ensureAccessToken()
    if (!token) {
      throw new ApiClientError(
        { code: 'Unauthorized', message: 'Session expired. Please sign in again.', httpStatus: 401 },
      )
    }
    headers.Authorization = `Bearer ${token}`
  }

  const url = path.startsWith('http') ? path : `${API_V1}${path.startsWith('/') ? path : `/${path}`}`

  const execute = async (authHeaders: Record<string, string>) => {
    return fetch(url, {
      method,
      headers: { ...headers, ...authHeaders },
      body: body !== undefined ? JSON.stringify(body) : undefined,
      credentials: 'same-origin',
      signal,
    })
  }

  let response = await execute(
    anonymous ? {} : { Authorization: headers.Authorization ?? '' },
  )

  if (response.status === 401 && !anonymous) {
    const newToken = await refreshAccessToken()
    if (newToken) {
      response = await execute({ Authorization: `Bearer ${newToken}` })
    } else {
      useAuthStore.getState().clearSession()
    }
  }

  const replayed = response.headers.get('Idempotent-Replay')
  const envelope = (await response.json()) as ApiResponse<T>

  if (!response.ok || !envelope.success) {
    if (envelope.error) {
      throw new ApiClientError(envelope.error)
    }
    throw new ApiClientError({
      code: 'HttpError',
      message: `HTTP ${response.status}`,
      httpStatus: response.status,
    })
  }

  if (envelope.data === null || envelope.data === undefined) {
    if (method === 'GET') {
      throw new ApiClientError({
        code: 'EmptyResponse',
        message: 'Expected response data was missing.',
        httpStatus: response.status,
      })
    }
    return undefined as T
  }

  if (replayed === 'true') {
    Object.defineProperty(envelope.data as object, '__idempotentReplay', {
      value: true,
      enumerable: false,
    })
  }

  return envelope.data
}

/** Login may return 200 (tokens) or 202 (2FA challenge) — handled outside apiRequest. */
export async function apiRaw<T>(path: string, init: RequestInit): Promise<{
  status: number
  envelope: ApiResponse<T>
}> {
  const url = `${API_V1}${path.startsWith('/') ? path : `/${path}`}`
  const response = await fetch(url, {
    ...init,
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...init.headers,
    },
  })
  const envelope = (await response.json()) as ApiResponse<T>
  return { status: response.status, envelope }
}
