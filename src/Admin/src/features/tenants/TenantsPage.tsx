import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { CreditCard, Plus } from 'lucide-react'
import {
  assignTenantSubscription,
  createTenant,
  listTenants,
} from '@/shared/api/admin-api'
import { TenantAssignPlanDialog } from '@/features/tenants/TenantAssignPlanDialog'
import { TenantFormDialog } from '@/features/tenants/TenantFormDialog'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { DataTablePagination } from '@/shared/components/patterns/DataTablePagination'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Badge } from '@/shared/components/ui/Badge'
import { Button } from '@/shared/components/ui/Button'
import { formatDateTimeOrDash, toast, toastApiError } from '@/shared/lib'
import { usePagination } from '@/shared/hooks/usePagination'
import type { TenantSummaryDto } from '@/shared/types/api'

const PAGE_SIZE = 20

function subscriptionBadgeTone(status: string | null): 'default' | 'success' | 'warning' | 'danger' {
  if (!status) return 'default'
  if (status === 'Active' || status === 'Trialing') return 'success'
  if (status === 'PastDue') return 'warning'
  if (status === 'Canceled') return 'danger'
  return 'default'
}

export function TenantsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { page, setPage } = usePagination()
  const [createOpen, setCreateOpen] = useState(false)
  const [assignTenant, setAssignTenant] = useState<TenantSummaryDto | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.tenants.list(page),
    queryFn: () => listTenants(page, PAGE_SIZE),
  })

  const tenants = data ?? []
  const hasNextPage = tenants.length === PAGE_SIZE

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all() })
  }

  const createMutation = useMutation({
    mutationFn: createTenant,
    onSuccess: () => {
      invalidate()
      toast.success(t('tenants.createdToast'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const assignMutation = useMutation({
    mutationFn: ({ tenantId, planCode }: { tenantId: string; planCode: string }) =>
      assignTenantSubscription(tenantId, planCode),
    onSuccess: () => {
      invalidate()
      toast.success(t('tenants.planAssigned'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  return (
    <PageContent>
      <PageHeader
        title={t('tenants.title')}
        description={t('tenants.subtitle')}
        actions={
          <Button onClick={() => setCreateOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            {t('tenants.create')}
          </Button>
        }
      />

      {isLoading ? <p className="text-sm text-slate-400">{t('common.loading')}</p> : null}
      {error ? <p className="text-sm text-red-400">{(error as Error).message}</p> : null}

      <Card>
        <CardBody className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead>
              <tr className="border-b border-slate-700 text-slate-400">
                <th className="py-2 pr-4">{t('tenants.slug')}</th>
                <th className="py-2 pr-4">{t('tenants.displayName')}</th>
                <th className="py-2 pr-4">{t('tenants.status')}</th>
                <th className="py-2 pr-4">{t('tenants.subscription')}</th>
                <th className="py-2 pr-4">{t('tenants.owner')}</th>
                <th className="py-2 pr-4">{t('tenants.created')}</th>
                <th className="py-2">{t('tenants.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {tenants.length === 0 && !isLoading ? (
                <tr>
                  <td colSpan={7} className="py-4 text-slate-400">
                    {t('tenants.empty')}
                  </td>
                </tr>
              ) : null}
              {tenants.map((tenant) => (
                <tr key={tenant.id} className="border-b border-slate-800">
                  <td className="py-2 pr-4 font-mono text-xs text-white">{tenant.slug}</td>
                  <td className="py-2 pr-4 text-white">{tenant.displayName}</td>
                  <td className="py-2 pr-4">
                    <Badge tone={tenant.status === 'Active' ? 'success' : 'warning'}>
                      {tenant.status}
                    </Badge>
                  </td>
                  <td className="py-2 pr-4">
                    {tenant.subscriptionPlanCode ? (
                      <span className="flex flex-wrap items-center gap-2">
                        <span className="text-slate-300">{tenant.subscriptionPlanCode}</span>
                        <Badge tone={subscriptionBadgeTone(tenant.subscriptionStatus)}>
                          {tenant.subscriptionStatus ?? '—'}
                        </Badge>
                      </span>
                    ) : (
                      <span className="text-slate-500">{t('tenants.noSubscription')}</span>
                    )}
                  </td>
                  <td className="py-2 pr-4">
                    <Link
                      to={`/users/${tenant.ownerUserId}`}
                      className="font-mono text-xs text-sky-400 hover:underline"
                    >
                      {tenant.ownerUserId.slice(0, 8)}…
                    </Link>
                  </td>
                  <td className="py-2 pr-4 text-slate-400">
                    {formatDateTimeOrDash(tenant.createdAtUtc)}
                  </td>
                  <td className="py-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => {
                        setAssignTenant(tenant)
                      }}
                    >
                      <CreditCard className="mr-1 h-3.5 w-3.5" />
                      {tenant.subscriptionPlanCode
                        ? t('tenants.changePlan')
                        : t('tenants.assignPlan')}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>

      <DataTablePagination
        page={page}
        totalPages={hasNextPage ? page + 1 : page}
        onPageChange={setPage}
      />

      <TenantFormDialog
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onSubmit={async (values) => {
          await createMutation.mutateAsync({
            slug: values.slug,
            displayName: values.displayName,
            ownerUserId: values.ownerUserId,
            publicTagline: values.publicTagline || null,
            publicDescription: values.publicDescription || null,
          })
        }}
      />

      <TenantAssignPlanDialog
        open={assignTenant !== null}
        tenant={assignTenant}
        onClose={() => setAssignTenant(null)}
        onSubmit={async (planCode) => {
          if (!assignTenant) return
          await assignMutation.mutateAsync({ tenantId: assignTenant.id, planCode })
        }}
      />
    </PageContent>
  )
}
