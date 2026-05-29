import { API_V1 } from '@/config/env'
import { apiFormRequest, apiRequest } from '@/shared/api/client'
import { useAuthStore } from '@/shared/stores/auth-store'
import type {
  AdminAuditLogListDto,
  AdminDashboardMetricsDto,
  AdminUserDetailDto,
  AdminUserListDto,
  AuthResponseDto,
  BooleanSettingDto,
  DeadLetterActionResult,
  DeadLetterMessage,
  DatabaseBackupItemDto,
  DatabaseBackupSettingsDto,
  ImportDatabaseBackupRestoreResponse,
  ImportSystemBackupRestoreResponse,
  RunDatabaseBackupResponse,
  RunSystemBackupResponse,
  SupportTicketDetailDto,
  SupportTicketListDto,
  SupportTicketPriority,
  SupportTicketStatus,
  SystemBackupItemDto,
  SystemBackupSettingsDto,
  CreditPackageDto,
  PlanDto,
  BillingInvoiceDto,
  TenantSummaryDto,
  SubscriptionSummaryDto,
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

export function getSystemBackupSettings() {
  return apiRequest<SystemBackupSettingsDto>('/admin/settings/system-backup')
}

export function setSystemBackupSettings(body: { enabled: boolean; retentionDays: number }) {
  return apiRequest<SystemBackupSettingsDto>('/admin/settings/system-backup', {
    method: 'PUT',
    body,
  })
}

export function runSystemBackupNow() {
  return apiRequest<RunSystemBackupResponse>('/admin/settings/system-backup/run', {
    method: 'POST',
    idempotent: true,
  })
}

export function listSystemBackupHistory(take = 20) {
  return apiRequest<SystemBackupItemDto[]>(`/admin/settings/system-backup/history?take=${take}`)
}

export function restoreSystemBackup(key: string, confirmation: string) {
  return apiRequest<boolean>('/admin/settings/system-backup/restore', {
    method: 'POST',
    body: { key, confirmation },
    idempotent: true,
  })
}

export async function downloadSystemBackup(key: string): Promise<Blob> {
  const token = useAuthStore.getState().accessToken
  const response = await fetch(
    `${API_V1}/admin/settings/system-backup/download?key=${encodeURIComponent(key)}`,
    {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    },
  )
  if (!response.ok) throw new Error('Download failed')
  return response.blob()
}

export function importRestoreSystemBackup(file: File, confirmation: string) {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('confirmation', confirmation)
  return apiFormRequest<ImportSystemBackupRestoreResponse>(
    '/admin/settings/system-backup/import-restore',
    formData,
    { idempotent: true },
  )
}

export function getDatabaseBackupSettings() {
  return apiRequest<DatabaseBackupSettingsDto>('/admin/settings/database-backup')
}

export function setDatabaseBackupSettings(body: { enabled: boolean; retentionDays: number }) {
  return apiRequest<DatabaseBackupSettingsDto>('/admin/settings/database-backup', {
    method: 'PUT',
    body,
  })
}

export function runDatabaseBackupNow() {
  return apiRequest<RunDatabaseBackupResponse>('/admin/settings/database-backup/run', {
    method: 'POST',
    idempotent: true,
  })
}

export function listDatabaseBackupHistory(take = 20) {
  return apiRequest<DatabaseBackupItemDto[]>(`/admin/settings/database-backup/history?take=${take}`)
}

export function restoreDatabaseBackup(key: string, confirmation: string) {
  return apiRequest<boolean>('/admin/settings/database-backup/restore', {
    method: 'POST',
    body: { key, confirmation },
    idempotent: true,
  })
}

export async function downloadDatabaseBackup(key: string): Promise<Blob> {
  const token = useAuthStore.getState().accessToken
  const response = await fetch(
    `${API_V1}/admin/settings/database-backup/download?key=${encodeURIComponent(key)}`,
    {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    },
  )
  if (!response.ok) throw new Error('Download failed')
  return response.blob()
}

export function importRestoreDatabaseBackup(file: File, confirmation: string) {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('confirmation', confirmation)
  return apiFormRequest<ImportDatabaseBackupRestoreResponse>(
    '/admin/settings/database-backup/import-restore',
    formData,
    { idempotent: true },
  )
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

export async function exportAuditLogsCsv(params: {
  fromUtc?: string
  toUtc?: string
  targetUserId?: string
  maxRows?: number
}): Promise<Blob> {
  const q = new URLSearchParams()
  if (params.fromUtc) q.set('fromUtc', params.fromUtc)
  if (params.toUtc) q.set('toUtc', params.toUtc)
  if (params.targetUserId) q.set('targetUserId', params.targetUserId)
  if (params.maxRows) q.set('maxRows', String(params.maxRows))
  const token = useAuthStore.getState().accessToken
  const response = await fetch(`${API_V1}/admin/audit-logs/export?${q.toString()}`, {
    headers: token ? { Authorization: `Bearer ${token}`, Accept: 'text/csv' } : { Accept: 'text/csv' },
  })
  if (!response.ok) throw new Error('Export failed')
  return response.blob()
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

export function listSupportTickets(params: {
  page?: number
  pageSize?: number
  search?: string
  status?: SupportTicketStatus
  priority?: SupportTicketPriority
  assignedAdminUserId?: string
} = {}) {
  const q = new URLSearchParams()
  if (params.page) q.set('page', String(params.page))
  if (params.pageSize) q.set('pageSize', String(params.pageSize))
  if (params.search) q.set('search', params.search)
  if (params.status) q.set('status', params.status)
  if (params.priority) q.set('priority', params.priority)
  if (params.assignedAdminUserId) q.set('assignedAdminUserId', params.assignedAdminUserId)
  const query = q.toString()
  return apiRequest<SupportTicketListDto>(`/admin/support/tickets${query ? `?${query}` : ''}`)
}

export function getSupportTicketById(ticketId: string) {
  return apiRequest<SupportTicketDetailDto>(`/admin/support/tickets/${ticketId}`)
}

export function assignSupportTicket(ticketId: string, assignedAdminUserId: string | null) {
  return apiRequest<boolean>(`/admin/support/tickets/${ticketId}/assign`, {
    method: 'POST',
    body: { assignedAdminUserId },
    idempotent: true,
  })
}

export function changeSupportTicketStatus(ticketId: string, status: SupportTicketStatus) {
  return apiRequest<boolean>(`/admin/support/tickets/${ticketId}/status`, {
    method: 'POST',
    body: { status },
    idempotent: true,
  })
}

export function changeSupportTicketPriority(ticketId: string, priority: SupportTicketPriority) {
  return apiRequest<boolean>(`/admin/support/tickets/${ticketId}/priority`, {
    method: 'POST',
    body: { priority },
    idempotent: true,
  })
}

export function listPlans() {
  return apiRequest<PlanDto[]>('/platform/plans')
}

export function createPlan(body: {
  code: string
  name: string
  description?: string | null
  priceMonthlyCents: number
  currency: string
  trialDays: number
  limits: PlanDto['limits']
  sortOrder: number
}) {
  return apiRequest<PlanDto>('/platform/plans', { method: 'POST', body, idempotent: true })
}

export function updatePlan(
  planId: string,
  body: {
    name: string
    description?: string | null
    priceMonthlyCents: number
    currency: string
    trialDays: number
    limits: PlanDto['limits']
    isActive: boolean
    sortOrder: number
  },
) {
  return apiRequest<PlanDto>(`/platform/plans/${planId}`, { method: 'PUT', body, idempotent: true })
}

export function deletePlan(planId: string) {
  return apiRequest<boolean>(`/platform/plans/${planId}`, { method: 'DELETE', idempotent: true })
}

export function listCreditPackages() {
  return apiRequest<CreditPackageDto[]>('/platform/credit-packages')
}

export function createCreditPackage(body: {
  code: string
  name: string
  description?: string | null
  priceCents: number
  currency: string
  creditsAmount: number
  sortOrder: number
}) {
  return apiRequest<CreditPackageDto>('/platform/credit-packages', { method: 'POST', body, idempotent: true })
}

export function updateCreditPackage(
  packageId: string,
  body: {
    name: string
    description?: string | null
    priceCents: number
    currency: string
    creditsAmount: number
    isActive: boolean
    sortOrder: number
  },
) {
  return apiRequest<CreditPackageDto>(`/platform/credit-packages/${packageId}`, {
    method: 'PUT',
    body,
    idempotent: true,
  })
}

export function deleteCreditPackage(packageId: string) {
  return apiRequest<boolean>(`/platform/credit-packages/${packageId}`, {
    method: 'DELETE',
    idempotent: true,
  })
}

export function listPlatformInvoices(page = 1, pageSize = 50) {
  return apiRequest<BillingInvoiceDto[]>(`/platform/invoices?page=${page}&pageSize=${pageSize}`)
}

export function listTenants(page = 1, pageSize = 20) {
  return apiRequest<TenantSummaryDto[]>(`/platform/tenants?page=${page}&pageSize=${pageSize}`)
}

export function createTenant(body: {
  slug: string
  displayName: string
  ownerUserId: string
  publicTagline?: string | null
  publicDescription?: string | null
}) {
  return apiRequest<TenantSummaryDto>('/platform/tenants', { method: 'POST', body, idempotent: true })
}

export function getTenantSubscription(tenantId: string) {
  return apiRequest<SubscriptionSummaryDto>(`/platform/tenants/${tenantId}/subscription`)
}

export function assignTenantSubscription(tenantId: string, planCode: string) {
  return apiRequest<SubscriptionSummaryDto>(`/platform/tenants/${tenantId}/subscription`, {
    method: 'POST',
    body: { planCode },
    idempotent: true,
  })
}

export function addSupportTicketMessage(
  ticketId: string,
  body: { body: string; isInternalNote?: boolean; attachments?: Array<{ storageKey: string; fileName: string; contentType: string; byteSize: number }> },
) {
  return apiRequest<boolean>(`/admin/support/tickets/${ticketId}/messages`, {
    method: 'POST',
    body,
    idempotent: true,
  })
}

export function broadcastMemberNotification(body: {
  title: string
  body: string
  linkUrl?: string | null
}) {
  return apiRequest<{ recipientCount: number }>('/admin/notifications/broadcast', {
    method: 'POST',
    body,
    idempotent: true,
  })
}
