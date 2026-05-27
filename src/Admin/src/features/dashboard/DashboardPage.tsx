import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Users } from 'lucide-react'
import { getDashboardMetrics } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Alert } from '@/shared/components/ui/Alert'
import { dash, formatDate } from '@/shared/lib'

export function DashboardPage() {
  const { t } = useTranslation()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.metrics.dashboard(),
    queryFn: getDashboardMetrics,
  })

  const maxReg =
    data?.registrationsLast14Days.reduce((m, d) => Math.max(m, d.count), 0) ?? 1

  return (
    <>
      <PageHeader title={t('dashboard.title')} description={t('dashboard.description')} />
      <PageContent>
        {error ? <Alert variant="error">{t('dashboard.metricsError')}</Alert> : null}

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Card>
            <CardBody className="flex items-center gap-4">
              <div className="flex size-12 items-center justify-center rounded-lg bg-blue-500/20">
                <Users className="size-6 text-blue-400" />
              </div>
              <div>
                <p className="text-sm text-slate-400">{t('dashboard.activeUsers')}</p>
                <p className="text-3xl font-semibold text-white">
                  {isLoading ? '…' : (data?.totalActiveUsers ?? dash())}
                </p>
              </div>
            </CardBody>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">{t('dashboard.registrationsTitle')}</h2>
          </CardHeader>
          <CardBody>
            {isLoading ? (
              <p className="text-sm text-slate-400">{t('dashboard.loadingChart')}</p>
            ) : data?.registrationsLast14Days.length ? (
              <ul className="space-y-2">
                {data.registrationsLast14Days.map((day) => (
                  <li key={day.day} className="flex items-center gap-3 text-sm">
                    <span className="w-24 shrink-0 text-slate-400">{formatDate(day.day)}</span>
                    <div className="h-2 flex-1 overflow-hidden rounded-full bg-[var(--color-surface-muted)]">
                      <div
                        className="h-full rounded-full bg-[var(--color-accent)]"
                        style={{ width: `${(day.count / maxReg) * 100}%` }}
                      />
                    </div>
                    <span className="w-8 text-right font-medium text-slate-200">
                      {day.count}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-slate-400">{t('dashboard.noRegistrationData')}</p>
            )}
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
