import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  getAllowPublicRegister,
  getDatabaseBackupSettings,
  getSystemBackupSettings,
  listDatabaseBackupHistory,
  listSystemBackupHistory,
  restoreDatabaseBackup,
  restoreSystemBackup,
  runDatabaseBackupNow,
  setDatabaseBackupSettings,
  runSystemBackupNow,
  setAllowPublicRegister,
  setSystemBackupSettings,
} from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { toast, toastApiError } from '@/shared/lib'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Alert } from '@/shared/components/ui/Alert'
import { ApiClientError } from '@/shared/lib/errors'

export function SettingsPage() {
  const settingsRestoreToken = 'RESTORE_SETTINGS'
  const databaseRestoreToken = 'RESTORE_DATABASE'
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.settings.allowPublicRegister(),
    queryFn: getAllowPublicRegister,
  })
  const backupSettingsQuery = useQuery({
    queryKey: queryKeys.settings.systemBackup(),
    queryFn: getSystemBackupSettings,
  })
  const backupHistoryQuery = useQuery({
    queryKey: queryKeys.settings.systemBackupHistory(20),
    queryFn: () => listSystemBackupHistory(20),
  })
  const databaseBackupSettingsQuery = useQuery({
    queryKey: queryKeys.settings.databaseBackup(),
    queryFn: getDatabaseBackupSettings,
  })
  const databaseBackupHistoryQuery = useQuery({
    queryKey: queryKeys.settings.databaseBackupHistory(20),
    queryFn: () => listDatabaseBackupHistory(20),
  })

  const mutation = useMutation({
    mutationFn: setAllowPublicRegister,
    onSuccess: (_data, enabled) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.settings.allowPublicRegister(),
      })
      toast.success(
        enabled ? t('toast.publicRegEnabled') : t('toast.publicRegDisabled'),
      )
    },
    onError: (err) => toastApiError(err, 'toast.settingsUpdateFailed'),
  })
  const backupConfigMutation = useMutation({
    mutationFn: setSystemBackupSettings,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings.systemBackup() })
      toast.success(t('settings.backupConfigUpdated'))
    },
    onError: (err) => toastApiError(err, 'toast.settingsUpdateFailed'),
  })
  const backupRunMutation = useMutation({
    mutationFn: runSystemBackupNow,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings.systemBackupHistory(20) })
      toast.success(t('settings.backupStarted'))
    },
    onError: (err) => toastApiError(err, 'settings.backupRunFailed'),
  })
  const backupRestoreMutation = useMutation({
    mutationFn: ({ key, confirmation }: { key: string; confirmation: string }) =>
      restoreSystemBackup(key, confirmation),
    onSuccess: () => {
      toast.success(t('settings.backupRestored'))
    },
    onError: (err) => toastApiError(err, 'settings.backupRestoreFailed'),
  })
  const databaseBackupConfigMutation = useMutation({
    mutationFn: setDatabaseBackupSettings,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings.databaseBackup() })
      toast.success(t('settings.databaseBackupConfigUpdated'))
    },
    onError: (err) => toastApiError(err, 'toast.settingsUpdateFailed'),
  })
  const databaseBackupRunMutation = useMutation({
    mutationFn: runDatabaseBackupNow,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings.databaseBackupHistory(20) })
      toast.success(t('settings.databaseBackupStarted'))
    },
    onError: (err) => toastApiError(err, 'settings.databaseBackupRunFailed'),
  })
  const databaseBackupRestoreMutation = useMutation({
    mutationFn: ({ key, confirmation }: { key: string; confirmation: string }) =>
      restoreDatabaseBackup(key, confirmation),
    onSuccess: () => {
      toast.success(t('settings.databaseBackupRestored'))
    },
    onError: (err) => toastApiError(err, 'settings.databaseBackupRestoreFailed'),
  })

  const enabled = data?.value ?? false
  const backupEnabled = backupSettingsQuery.data?.enabled ?? false
  const backupRetentionDays = backupSettingsQuery.data?.retentionDays ?? 7
  const databaseBackupEnabled = databaseBackupSettingsQuery.data?.enabled ?? false
  const databaseBackupRetentionDays = databaseBackupSettingsQuery.data?.retentionDays ?? 7

  return (
    <>
      <PageHeader title={t('settings.title')} description={t('settings.description')} />
      <PageContent className="max-w-xl">
        {error ? (
          <Alert variant="error">
            {error instanceof ApiClientError ? error.message : t('settings.loadFailed')}
          </Alert>
        ) : null}

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">{t('settings.publicRegistration')}</h2>
            <p className="mt-1 text-sm text-slate-400">{t('settings.publicRegistrationHelp')}</p>
          </CardHeader>
          <CardBody>
            <label className="flex cursor-pointer items-center justify-between gap-4">
              <span className="text-sm text-slate-200">
                {isLoading
                  ? t('common.loading')
                  : enabled
                    ? t('common.enabled')
                    : t('common.disabled')}
              </span>
              <input
                type="checkbox"
                className="size-5 rounded border-[var(--color-border)] accent-[var(--color-accent)]"
                checked={enabled}
                disabled={isLoading || mutation.isPending}
                onChange={(e) => mutation.mutate(e.target.checked)}
              />
            </label>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">{t('settings.systemBackup')}</h2>
            <p className="mt-1 text-sm text-slate-400">{t('settings.systemBackupHelp')}</p>
          </CardHeader>
          <CardBody className="space-y-4">
            <label className="flex cursor-pointer items-center justify-between gap-4">
              <span className="text-sm text-slate-200">{t('settings.backupEnabled')}</span>
              <input
                type="checkbox"
                className="size-5 rounded border-[var(--color-border)] accent-[var(--color-accent)]"
                checked={backupEnabled}
                disabled={backupSettingsQuery.isLoading || backupConfigMutation.isPending}
                onChange={(e) =>
                  backupConfigMutation.mutate({
                    enabled: e.target.checked,
                    retentionDays: backupRetentionDays,
                  })
                }
              />
            </label>
            <label className="flex items-center justify-between gap-4">
              <span className="text-sm text-slate-200">{t('settings.backupRetentionDays')}</span>
              <input
                type="number"
                min={1}
                max={365}
                className="w-28 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-soft)] px-3 py-2 text-sm text-slate-100"
                value={backupRetentionDays}
                disabled={backupSettingsQuery.isLoading || backupConfigMutation.isPending}
                onChange={(e) =>
                  backupConfigMutation.mutate({
                    enabled: backupEnabled,
                    retentionDays: Math.max(1, Number(e.target.value) || 7),
                  })
                }
              />
            </label>
            <button
              type="button"
              className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
              onClick={() => backupRunMutation.mutate()}
              disabled={backupRunMutation.isPending}
            >
              {backupRunMutation.isPending ? t('common.loading') : t('settings.runBackupNow')}
            </button>

            <div className="space-y-2 border-t border-[var(--color-border)] pt-3">
              <p className="text-sm font-medium text-slate-100">{t('settings.backupHistory')}</p>
              {backupHistoryQuery.data?.length ? (
                <div className="space-y-2">
                  {backupHistoryQuery.data.map((item) => (
                    <div
                      key={item.key}
                      className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-soft)] px-3 py-2"
                    >
                      <div className="min-w-0">
                        <p className="truncate text-xs text-slate-300">{item.key}</p>
                        <p className="text-xs text-slate-400">
                          {new Date(item.createdAtUtc).toLocaleString()} · {item.byteSize} bytes
                        </p>
                      </div>
                      <button
                        type="button"
                        className="rounded-md border border-[var(--color-border)] px-3 py-1 text-xs text-slate-100 disabled:opacity-60"
                        disabled={backupRestoreMutation.isPending}
                        onClick={() => {
                          const confirmation = window.prompt(
                            t('settings.confirmRestorePrompt', { token: settingsRestoreToken }),
                            '',
                          )?.trim()
                          if (!confirmation) return
                          backupRestoreMutation.mutate({ key: item.key, confirmation })
                        }}
                      >
                        {t('settings.restore')}
                      </button>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-slate-400">
                  {backupHistoryQuery.isLoading ? t('common.loading') : t('settings.noBackupsYet')}
                </p>
              )}
            </div>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">{t('settings.databaseBackup')}</h2>
            <p className="mt-1 text-sm text-slate-400">{t('settings.databaseBackupHelp')}</p>
          </CardHeader>
          <CardBody className="space-y-4">
            <label className="flex cursor-pointer items-center justify-between gap-4">
              <span className="text-sm text-slate-200">{t('settings.databaseBackupEnabled')}</span>
              <input
                type="checkbox"
                className="size-5 rounded border-[var(--color-border)] accent-[var(--color-accent)]"
                checked={databaseBackupEnabled}
                disabled={databaseBackupSettingsQuery.isLoading || databaseBackupConfigMutation.isPending}
                onChange={(e) =>
                  databaseBackupConfigMutation.mutate({
                    enabled: e.target.checked,
                    retentionDays: databaseBackupRetentionDays,
                  })
                }
              />
            </label>
            <label className="flex items-center justify-between gap-4">
              <span className="text-sm text-slate-200">{t('settings.databaseBackupRetentionDays')}</span>
              <input
                type="number"
                min={1}
                max={365}
                className="w-28 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-soft)] px-3 py-2 text-sm text-slate-100"
                value={databaseBackupRetentionDays}
                disabled={databaseBackupSettingsQuery.isLoading || databaseBackupConfigMutation.isPending}
                onChange={(e) =>
                  databaseBackupConfigMutation.mutate({
                    enabled: databaseBackupEnabled,
                    retentionDays: Math.max(1, Number(e.target.value) || 7),
                  })
                }
              />
            </label>
            <button
              type="button"
              className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
              onClick={() => databaseBackupRunMutation.mutate()}
              disabled={databaseBackupRunMutation.isPending}
            >
              {databaseBackupRunMutation.isPending ? t('common.loading') : t('settings.runDatabaseBackupNow')}
            </button>
            <div className="space-y-2 border-t border-[var(--color-border)] pt-3">
              <p className="text-sm font-medium text-slate-100">{t('settings.databaseBackupHistory')}</p>
              {databaseBackupHistoryQuery.data?.length ? (
                <div className="space-y-2">
                  {databaseBackupHistoryQuery.data.map((item) => (
                    <div
                      key={item.key}
                      className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-[var(--color-border)] bg-[var(--color-surface-soft)] px-3 py-2"
                    >
                      <div className="min-w-0">
                        <p className="truncate text-xs text-slate-300">{item.key}</p>
                        <p className="text-xs text-slate-400">
                          {new Date(item.createdAtUtc).toLocaleString()} · {item.byteSize} bytes
                        </p>
                      </div>
                      <button
                        type="button"
                        className="rounded-md border border-[var(--color-border)] px-3 py-1 text-xs text-slate-100 disabled:opacity-60"
                        disabled={databaseBackupRestoreMutation.isPending}
                        onClick={() => {
                          const confirmation = window.prompt(
                            t('settings.confirmRestorePrompt', { token: databaseRestoreToken }),
                            '',
                          )?.trim()
                          if (!confirmation) return
                          databaseBackupRestoreMutation.mutate({ key: item.key, confirmation })
                        }}
                      >
                        {t('settings.restore')}
                      </button>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-slate-400">
                  {databaseBackupHistoryQuery.isLoading ? t('common.loading') : t('settings.noDatabaseBackupsYet')}
                </p>
              )}
            </div>
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
