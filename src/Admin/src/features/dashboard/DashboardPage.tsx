import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Building2,
  LifeBuoy,
  Lock,
  Mail,
  UserPlus,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { getDashboardMetrics } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Alert } from '@/shared/components/ui/Alert'
import { dash, formatDate } from '@/shared/lib'
import { cn } from '@/shared/lib/cn'

type MetricCardProps = {
  to: string
  label: string
  value: string | number
  icon: LucideIcon
  iconClassName: string
  highlight?: boolean
}

function MetricCard({ to, label, value, icon: Icon, iconClassName, highlight }: MetricCardProps) {
  return (
    <Link
      to={to}
      className={cn(
        'block rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-elevated)] transition-colors hover:border-[var(--color-accent)]/40',
        highlight && 'ring-1 ring-amber-500/30',
      )}
    >
      <Card className="border-0 bg-transparent shadow-none">
        <CardBody className="flex items-center gap-4">
          <div
            className={cn(
              'flex size-12 shrink-0 items-center justify-center rounded-lg',
              iconClassName,
            )}
          >
            <Icon className="size-6" />
          </div>
          <div className="min-w-0">
            <p className="text-sm text-slate-400">{label}</p>
            <p className="text-3xl font-semibold text-white">{value}</p>
          </div>
        </CardBody>
      </Card>
    </Link>
  )
}

export function DashboardPage() {
  const { t } = useTranslation()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.metrics.dashboard(),
    queryFn: getDashboardMetrics,
  })

  const maxReg =
    data?.registrationsLast14Days.reduce((m, d) => Math.max(m, d.count), 0) ?? 1

  const val = (n: number | undefined) => (isLoading ? '…' : (n ?? dash()))

  const metrics: MetricCardProps[] = [
    {
      to: '/users',
      label: t('dashboard.activeUsers'),
      value: val(data?.totalActiveUsers),
      icon: Users,
      iconClassName: 'bg-blue-500/20 text-blue-400',
    },
    {
      to: '/users',
      label: t('dashboard.lockedUsers'),
      value: val(data?.lockedUsers),
      icon: Lock,
      iconClassName: 'bg-rose-500/20 text-rose-400',
      highlight: (data?.lockedUsers ?? 0) > 0,
    },
    {
      to: '/users',
      label: t('dashboard.registrationsToday'),
      value: val(data?.registrationsToday),
      icon: UserPlus,
      iconClassName: 'bg-emerald-500/20 text-emerald-400',
    },
    {
      to: '/users',
      label: t('dashboard.registrationsLast7Days'),
      value: val(data?.registrationsLast7Days),
      icon: UserPlus,
      iconClassName: 'bg-teal-500/20 text-teal-400',
    },
    {
      to: '/tenants',
      label: t('dashboard.totalTenants'),
      value: val(data?.totalTenants),
      icon: Building2,
      iconClassName: 'bg-violet-500/20 text-violet-400',
    },
    {
      to: '/support',
      label: t('dashboard.openSupportTickets'),
      value: val(data?.openSupportTickets),
      icon: LifeBuoy,
      iconClassName: 'bg-cyan-500/20 text-cyan-400',
      highlight: (data?.openSupportTickets ?? 0) > 0,
    },
    {
      to: '/contact-messages',
      label: t('dashboard.unreadContactMessages'),
      value: val(data?.unreadContactMessages),
      icon: Mail,
      iconClassName: 'bg-amber-500/20 text-amber-400',
      highlight: (data?.unreadContactMessages ?? 0) > 0,
    },
  ]

  return (
    <>
      <PageHeader title={t('dashboard.title')} description={t('dashboard.description')} />
      <PageContent className="space-y-6">
        {error ? <Alert variant="error">{t('dashboard.metricsError')}</Alert> : null}

        <section>
          <h2 className="mb-3 text-sm font-medium text-slate-300">{t('dashboard.kpiTitle')}</h2>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
            {metrics.map((m) => (
              <MetricCard key={m.label} {...m} />
            ))}
          </div>
          <p className="mt-2 text-xs text-slate-500">{t('dashboard.kpiHint')}</p>
        </section>

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
