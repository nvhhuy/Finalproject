import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { createPlan, deletePlan, listPlans, updatePlan } from '@/shared/api/admin-api'
import { PlanFormDialog } from '@/features/plans/PlanFormDialog'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Badge } from '@/shared/components/ui/Badge'
import { Button } from '@/shared/components/ui/Button'
import { confirmAction, toast, toastApiError } from '@/shared/lib'
import type { PlanDto } from '@/shared/types/api'

function formatPrice(cents: number, currency: string) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(cents / 100)
}

function defaultLimits(maxUsers: number, existing?: PlanDto['limits']): PlanDto['limits'] {
  return {
    maxUsers,
    maxStorageBytes: existing?.maxStorageBytes ?? 2_147_483_648,
    modules: existing?.modules ?? ['support', 'media'],
    maxOpenSupportTickets: existing?.maxOpenSupportTickets ?? 20,
    adminSeats: existing?.adminSeats ?? 1,
    impersonation: existing?.impersonation ?? false,
    backupRestore: existing?.backupRestore ?? false,
  }
}

export function PlansPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [editingPlan, setEditingPlan] = useState<PlanDto | undefined>()

  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.plans.list(),
    queryFn: () => listPlans(),
  })

  const plans = (data ?? []).slice().sort((a: PlanDto, b: PlanDto) => a.sortOrder - b.sortOrder)

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.plans.list() })
  }

  const createMutation = useMutation({
    mutationFn: createPlan,
    onSuccess: () => {
      invalidate()
      toast.success(t('plans.created'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const updateMutation = useMutation({
    mutationFn: ({ planId, body }: { planId: string; body: Parameters<typeof updatePlan>[1] }) =>
      updatePlan(planId, body),
    onSuccess: () => {
      invalidate()
      toast.success(t('plans.updated'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const deleteMutation = useMutation({
    mutationFn: deletePlan,
    onSuccess: () => {
      invalidate()
      toast.success(t('plans.deleted'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const openCreate = () => {
    setDialogMode('create')
    setEditingPlan(undefined)
    setDialogOpen(true)
  }

  const openEdit = (plan: PlanDto) => {
    setDialogMode('edit')
    setEditingPlan(plan)
    setDialogOpen(true)
  }

  const handleDelete = (plan: PlanDto) => {
    if (!confirmAction(t('plans.deleteConfirm', { name: plan.name }))) return
    void deleteMutation.mutateAsync(plan.id)
  }

  return (
    <PageContent>
      <PageHeader
        title={t('plans.title')}
        description={t('plans.subtitle')}
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-1 size-4" />
            {t('plans.create')}
          </Button>
        }
      />

      {isLoading ? <p className="text-sm text-slate-400">{t('common.loading')}</p> : null}
      {error ? <p className="text-sm text-red-400">{(error as Error).message}</p> : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {plans.map((plan: PlanDto) => (
          <Card key={plan.id}>
            <CardBody className="space-y-2">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="font-semibold text-white">{plan.name}</p>
                  <p className="text-xs text-slate-400">
                    {plan.code} · {plan.currency}
                  </p>
                </div>
                <div className="flex items-center gap-1">
                  <Badge tone={plan.isActive ? 'success' : 'default'}>
                    {plan.isActive ? t('common.enabled') : t('common.disabled')}
                  </Badge>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={t('plans.edit')}
                    onClick={() => openEdit(plan)}
                  >
                    <Pencil className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={t('plans.delete')}
                    onClick={() => handleDelete(plan)}
                    disabled={deleteMutation.isPending}
                  >
                    <Trash2 className="size-4 text-red-400" />
                  </Button>
                </div>
              </div>
              <p className="text-lg font-medium text-white">{formatPrice(plan.priceMonthlyCents, plan.currency)}</p>
              {plan.description ? <p className="text-sm text-slate-400">{plan.description}</p> : null}
              <ul className="text-xs text-slate-400">
                <li>{t('plans.maxUsers', { count: plan.limits.maxUsers })}</li>
                <li>{t('plans.trialDays', { count: plan.trialDays })}</li>
              </ul>
            </CardBody>
          </Card>
        ))}
      </div>

      <PlanFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        mode={dialogMode}
        plan={editingPlan}
        onSubmit={async (values) => {
          const limits = defaultLimits(values.maxUsers, editingPlan?.limits)
          if (dialogMode === 'create') {
            await createMutation.mutateAsync({
              code: values.code,
              name: values.name,
              description: values.description || null,
              priceMonthlyCents: values.priceMonthlyCents,
              currency: values.currency,
              trialDays: values.trialDays,
              limits,
              sortOrder: values.sortOrder,
            })
            return
          }
          if (!editingPlan) return
          await updateMutation.mutateAsync({
            planId: editingPlan.id,
            body: {
              name: values.name,
              description: values.description || null,
              priceMonthlyCents: values.priceMonthlyCents,
              currency: values.currency,
              trialDays: values.trialDays,
              limits,
              isActive: values.isActive,
              sortOrder: values.sortOrder,
            },
          })
        }}
      />
    </PageContent>
  )
}
