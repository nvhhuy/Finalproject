/** Mirrors backend `ApiResponse<T>`. */
export interface ApiResponse<T> {
  success: boolean
  data: T | null
  error: ApiErrorPayload | null
  meta: ApiMeta
}

export interface ApiErrorPayload {
  code: string
  message: string | null
  httpStatus: number | null
  validationErrors?: Record<string, string[]> | null
  extensions?: Record<string, unknown> | null
}

export interface ApiMeta {
  correlationId?: string | null
  timestampUtc?: string | null
}

export interface AuthResponseDto {
  userId: string
  userName: string
  email: string
  firstName: string
  lastName: string
  accessToken: string
  accessTokenExpiresUtc: string
  refreshToken: string
  refreshTokenExpiresUtc: string
}

export interface TwoFactorChallengeDto {
  pendingLoginId: string
  message: string | null
}

export interface AdminUserSummaryDto {
  id: string
  userName: string
  email: string
  role: string
  isLocked: boolean
  emailVerifiedAtUtc: string | null
  registeredAtUtc: string | null
  rowVersion: number
}

export interface AdminUserDetailDto extends AdminUserSummaryDto {
  firstName: string
  lastName: string
  lockReason: string | null
  twoFactorEnabled: boolean
  lastLoginAtUtc: string | null
  deletedAtUtc: string | null
  accountClosedAtUtc: string | null
}

export interface AdminUserListDto {
  items: AdminUserSummaryDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AdminAuditLogEntryDto {
  id: string
  actorUserId: string
  action: string
  targetUserId: string | null
  ipAddress: string | null
  details: string | null
  createdAtUtc: string
}

export interface AdminAuditLogListDto {
  items: AdminAuditLogEntryDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface DailyRegistrationDto {
  day: string
  count: number
}

export interface AdminDashboardMetricsDto {
  totalActiveUsers: number
  registrationsLast14Days: DailyRegistrationDto[]
}

export interface BooleanSettingDto {
  key: string
  value: boolean
}

export interface DeadLetterMessage {
  deliveryTag: number
  originalRoutingKey: string | null
  originalExchange: string | null
  correlationId: string | null
  retryCount: number
  lastFailureReason: string | null
  payloadPreview: string
}

export interface DeadLetterActionResult {
  queue: string
  action: string
  count: number
}

export const USER_ROLES = ['User', 'Admin'] as const
export type UserRole = (typeof USER_ROLES)[number]

export const DEFAULT_DLQ_QUEUE = 'media.uploaded.v1.dlq'
