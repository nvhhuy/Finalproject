export type AuthTokens = {
  accessToken: string;
  accessTokenExpiresUtc: string;
  refreshToken: string;
  refreshTokenExpiresUtc: string;
};

const STORAGE_KEY = "fp.auth.tokens";

export function loadTokens(): AuthTokens | null {
  if (typeof window === "undefined") return null;
  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as AuthTokens;
  } catch {
    return null;
  }
}

export function saveTokens(tokens: AuthTokens | null) {
  if (typeof window === "undefined") return;
  if (!tokens) {
    window.localStorage.removeItem(STORAGE_KEY);
    return;
  }
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(tokens));
}

