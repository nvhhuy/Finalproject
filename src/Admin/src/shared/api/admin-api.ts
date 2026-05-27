import { apiRequest } from '@/shared/api/client'
import type {
  AdminAuditLogListDto,
  AdminDashboardMetricsDto,
  AdminUserDetailDto,
  AdminUserListDto,
  AuthResponseDto,
  BooleanSettingDto,
  DeadLetterActionResult,
  DeadLetterMessage,
} from '@/shared/types/api'

export interface ListUsersParams {
  page?: number
  pageSize?: number
  /** Matches email OR username (case-insensitive). */
  search?: string
  emailContains?: string
  userNameContains?: string
  isLocked?: boolean
  role?: string
  sortBy?: 'username' | 'email' | 'registeredat' | 'role'
  sortDescending?: boolean
}

export function listUsers(params: ListUsersParams = {}) {
  const q = new URLSearchParams()
  if (params.page) q.set('page', String(params.page))
  if (params.pageSize) q.set('pageSize', String(params.pageSize))
  if (params.search) q.set('search', params.search)
  if (params.emailContains) q.set('emailContains', params.emailContains)
  if (params.userNameContains) q.set('userNameContains', params.userNameContains)
  if (params.isLocked !== undefined) q.set('isLocked', String(params.isLocked))
  if (params.role) q.set('role', params.role)
  if (params.sortBy) q.set('sortBy', params.sortBy)
  if (params.sortDescending) q.set('sortDescending', 'true')
  const query = q.toString()
  return apiRequest<AdminUserListDto>(`/admin/users${query ? `?${query}` : ''}`)
}

export function getUserById(userId: string) {
  return apiRequest<AdminUserDetailDto>(`/admin/users/${userId}`)
}

export function createUser(body: {
  firstName: string
  lastName: string
  userName: string
  email: string
  password: string
  role: string
}) {
  return apiRequest<string>('/admin/users', { method: 'POST', body, idempotent: true })
}

export function updateUser(
  userId: string,
  body: {
    firstName: string
    lastName: string
    userName: string
    email: string
    role: string
    rowVersion: number
  },
) {
  return apiRequest<boolean>(`/admin/users/${userId}`, {
    method: 'PUT',
    body,
    idempotent: true,
  })
}

export function deleteUser(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}?rowVersion=${rowVersion}`, {
    method: 'DELETE',
    idempotent: true,
  })
}

export function lockUser(userId: string, rowVersion: number, reason?: string) {
  return apiRequest<boolean>(`/admin/users/${userId}/lock`, {
    method: 'POST',
    body: { reason: reason ?? null, rowVersion },
    idempotent: true,
  })
}

export function unlockUser(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/unlock`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function impersonateUser(userId: string, rowVersion: number) {
  return apiRequest<AuthResponseDto>(`/admin/users/${userId}/impersonate`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function resetTwoFactor(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/2fa/reset`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function resetPassword(userId: string, newPassword: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/password/reset`, {
    method: 'POST',
    body: { newPassword, rowVersion },
    idempotent: true,
  })
}

export function markEmailVerified(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/email/verify`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function resendEmailVerification(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/email/resend-verification`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function revokeSessions(userId: string, rowVersion: number) {
  return apiRequest<boolean>(`/admin/users/${userId}/sessions/revoke`, {
    method: 'POST',
    body: { rowVersion },
    idempotent: true,
  })
}

export function getDashboardMetrics() {
  return apiRequest<AdminDashboardMetricsDto>('/admin/metrics/dashboard')
}

export function getAllowPublicRegister() {
  return apiRequest<BooleanSettingDto>('/admin/settings/allow-public-register')
}

export function setAllowPublicRegister(value: boolean) {
  return apiRequest<BooleanSettingDto>('/admin/settings/allow-public-register', {
    method: 'PUT',
    body: { value },
  })
}

export function listAuditLogs(params: {
  page?: number
  pageSize?: number
  targetUserId?: string
}) {
  const q = new URLSearchParams()
  if (params.page) q.set('page', String(params.page))
  if (params.pageSize) q.set('pageSize', String(params.pageSize))
  if (params.targetUserId) q.set('targetUserId', params.targetUserId)
  const query = q.toString()
  return apiRequest<AdminAuditLogListDto>(
    `/admin/audit-logs${query ? `?${query}` : ''}`,
  )
}

export function peekDlq(queueName: string, count = 10) {
  return apiRequest<DeadLetterMessage[]>(
    `/admin/messaging/dlq/${encodeURIComponent(queueName)}/peek?count=${count}`,
  )
}

export function replayDlq(queueName: string, count = 10) {
  return apiRequest<DeadLetterActionResult>(
    `/admin/messaging/dlq/${encodeURIComponent(queueName)}/replay?count=${count}`,
    { method: 'POST', idempotent: true },
  )
}

export function drainDlq(queueName: string, count = 10) {
  return apiRequest<DeadLetterActionResult>(
    `/admin/messaging/dlq/${encodeURIComponent(queueName)}/drain?count=${count}`,
    { method: 'POST', idempotent: true },
  )
}
