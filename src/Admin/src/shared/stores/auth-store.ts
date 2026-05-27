import { create } from 'zustand'
import type { AuthResponseDto } from '@/shared/types/api'
import { isAdminToken } from '@/shared/lib/jwt'

const REFRESH_STORAGE_KEY = 'fp_admin_refresh'

interface AuthUser {
  userId: string
  userName: string
  email: string
  firstName: string
  lastName: string
}

interface AuthState {
  accessToken: string | null
  accessTokenExpiresUtc: string | null
  user: AuthUser | null
  isHydrated: boolean
  setSession: (auth: AuthResponseDto) => void
  setAccessToken: (accessToken: string, expiresUtc: string) => void
  clearSession: () => void
  hydrate: () => void
  getRefreshToken: () => string | null
}

function persistRefreshToken(token: string) {
  sessionStorage.setItem(REFRESH_STORAGE_KEY, token)
}

function readRefreshToken(): string | null {
  return sessionStorage.getItem(REFRESH_STORAGE_KEY)
}

function clearRefreshToken() {
  sessionStorage.removeItem(REFRESH_STORAGE_KEY)
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  accessTokenExpiresUtc: null,
  user: null,
  isHydrated: false,

  setSession: (auth) => {
    if (!isAdminToken(auth.accessToken)) {
      throw new Error('This account does not have administrator access.')
    }
    persistRefreshToken(auth.refreshToken)
    set({
      accessToken: auth.accessToken,
      accessTokenExpiresUtc: auth.accessTokenExpiresUtc,
      user: {
        userId: auth.userId,
        userName: auth.userName,
        email: auth.email,
        firstName: auth.firstName,
        lastName: auth.lastName,
      },
    })
  },

  setAccessToken: (accessToken, expiresUtc) => {
    set({ accessToken, accessTokenExpiresUtc: expiresUtc })
  },

  clearSession: () => {
    clearRefreshToken()
    set({
      accessToken: null,
      accessTokenExpiresUtc: null,
      user: null,
    })
  },

  hydrate: () => {
    set({ isHydrated: true })
  },

  getRefreshToken: () => readRefreshToken(),
}))
