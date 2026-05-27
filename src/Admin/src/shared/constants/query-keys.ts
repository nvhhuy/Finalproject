/** TanStack Query keys — prefix with `admin` for consistent invalidation. */
export const queryKeys = {
  metrics: {
    dashboard: () => ['admin', 'metrics', 'dashboard'] as const,
  },
  users: {
    all: () => ['admin', 'users'] as const,
    list: (params: unknown) => ['admin', 'users', params] as const,
    detail: (id: string | null | undefined) => ['admin', 'users', id] as const,
  },
  settings: {
    allowPublicRegister: () => ['admin', 'settings', 'allow-public-register'] as const,
  },
  auditLogs: {
    list: (page: number, targetUserId: string) =>
      ['admin', 'audit-logs', page, targetUserId] as const,
    all: () => ['admin', 'audit-logs'] as const,
  },
  dlq: {
    all: () => ['admin', 'dlq'] as const,
    peek: (queueName: string, count: number) =>
      ['admin', 'dlq', queueName, count] as const,
  },
} as const
