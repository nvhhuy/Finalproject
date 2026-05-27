import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { getAllowPublicRegister, setAllowPublicRegister } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { toast, toastApiError } from '@/shared/lib'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Alert } from '@/shared/components/ui/Alert'
import { ApiClientError } from '@/shared/lib/errors'

export function SettingsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.settings.allowPublicRegister(),
    queryFn: getAllowPublicRegister,
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

  const enabled = data?.value ?? false

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
      </PageContent>
    </>
  )
}
