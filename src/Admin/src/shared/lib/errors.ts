import type { ApiErrorPayload } from '@/shared/types/api'

export class ApiClientError extends Error {
  readonly code: string
  readonly httpStatus: number | null
  readonly validationErrors: Record<string, string[]> | null

  constructor(payload: ApiErrorPayload, fallbackMessage = 'Request failed') {
    super(payload.message ?? fallbackMessage)
    this.name = 'ApiClientError'
    this.code = payload.code
    this.httpStatus = payload.httpStatus
    this.validationErrors = payload.validationErrors ?? null
  }
}

export function formatValidationErrors(
  errors: Record<string, string[]> | null,
): string {
  if (!errors) {
    return ''
  }
  return Object.entries(errors)
    .flatMap(([field, messages]) => messages.map((m) => `${field}: ${m}`))
    .join('\n')
}
