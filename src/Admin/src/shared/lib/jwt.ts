const ROLE_CLAIM =
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

/** Decode JWT payload without verifying signature (server validates on each request). */
export function parseJwtPayload(token: string): Record<string, unknown> {
  const parts = token.split('.')
  if (parts.length < 2) {
    throw new Error('Invalid token format')
  }
  const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  return JSON.parse(atob(padded)) as Record<string, unknown>
}

export function getTokenRoles(payload: Record<string, unknown>): string[] {
  const raw = payload.role ?? payload[ROLE_CLAIM]
  if (Array.isArray(raw)) {
    return raw.map(String)
  }
  if (raw != null && raw !== '') {
    return [String(raw)]
  }
  return []
}

export function isAdminToken(accessToken: string): boolean {
  try {
    const payload = parseJwtPayload(accessToken)
    return getTokenRoles(payload).includes('Admin')
  } catch {
    return false
  }
}

export function getTokenExpiryMs(accessToken: string): number | null {
  try {
    const payload = parseJwtPayload(accessToken)
    const exp = payload.exp
    if (typeof exp === 'number') {
      return exp * 1000
    }
    return null
  } catch {
    return null
  }
}
