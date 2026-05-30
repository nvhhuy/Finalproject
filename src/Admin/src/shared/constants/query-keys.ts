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
    systemBackup: () => ['admin', 'settings', 'system-backup'] as const,
    systemBackupHistory: (take = 20) => ['admin', 'settings', 'system-backup', 'history', take] as const,
    databaseBackup: () => ['admin', 'settings', 'database-backup'] as const,
    databaseBackupHistory: (take = 20) => ['admin', 'settings', 'database-backup', 'history', take] as const,
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
  plans: {
    all: () => ['admin', 'plans'] as const,
    list: () => ['admin', 'plans', 'list'] as const,
  },
  creditPackages: {
    all: () => ['admin', 'credit-packages'] as const,
    list: () => ['admin', 'credit-packages', 'list'] as const,
  },
  invoices: {
    all: () => ['admin', 'invoices'] as const,
    list: (page: number) => ['admin', 'invoices', 'list', page] as const,
  },
  tenants: {
    all: () => ['admin', 'tenants'] as const,
    list: (page: number) => ['admin', 'tenants', 'list', page] as const,
    subscription: (tenantId: string | null | undefined) =>
      ['admin', 'tenants', tenantId, 'subscription'] as const,
  },
  support: {
    all: () => ['admin', 'support'] as const,
    list: (params: unknown) => ['admin', 'support', 'list', params] as const,
    detail: (ticketId: string | null | undefined) => ['admin', 'support', 'detail', ticketId] as const,
  },
  contactMessages: {
    all: () => ['admin', 'contact-messages'] as const,
    list: (params: unknown) => ['admin', 'contact-messages', 'list', params] as const,
  },
} as const
