/** API base URL. Empty string = same origin (Vite dev proxy `/api`). */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export const API_V1 = `${API_BASE_URL}/api/v1`
