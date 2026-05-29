import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { listPlans } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { formSelectClass } from '@/shared/constants/styles'
import { Modal } from '@/shared/components/ui/Modal'
import { Button } from '@/shared/components/ui/Button'
import type { TenantSummaryDto } from '@/shared/types/api'

export function TenantAssignPlanDialog({
  open,
  tenant,
  onClose,
  onSubmit,
}: {
  open: boolean
  tenant: TenantSummaryDto | null
  onClose: () => void
  onSubmit: (planCode: string) => Promise<void>
}) {
  const { t } = useTranslation()
  const [planSelection, setPlanSelection] = useState<{ tenantId: string; value: string } | null>(
    null,
  )
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const { data: plans } = useQuery({
    queryKey: queryKeys.plans.list(),
    queryFn: () => listPlans(),
    enabled: open,
  })

  const activePlans = (plans ?? []).filter((p) => p.isActive)
  const selectedPlanCode =
    planSelection?.tenantId === tenant?.id
    ? (planSelection?.value ?? '')
      : (tenant?.subscriptionPlanCode ?? '')

  function handleClose() {
    setPlanSelection(null)
    setError(null)
    onClose()
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedPlanCode) return
    setError(null)
    setLoading(true)
    try {
      await onSubmit(selectedPlanCode)
      handleClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('errors.saveFailed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={t('tenants.assignPlanTitle', { name: tenant?.displayName ?? '' })}
    >
      {tenant ? (
        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
          <p className="text-sm text-slate-400">
            {tenant.subscriptionPlanCode
              ? t('tenants.assignPlanCurrent', {
                  plan: tenant.subscriptionPlanCode,
                  status: tenant.subscriptionStatus ?? '—',
                })
              : t('tenants.assignPlanNone')}
          </p>
          <label className="block text-sm text-slate-300">
            {t('tenants.planCode')}
            <select
              className={formSelectClass}
              value={selectedPlanCode}
              onChange={(e) => {
                if (!tenant) return
                setPlanSelection({ tenantId: tenant.id, value: e.target.value })
                setError(null)
              }}
              required
            >
              <option value="">{t('tenants.selectPlan')}</option>
              {activePlans.map((p) => (
                <option key={p.id} value={p.code}>
                  {p.name} ({p.code})
                </option>
              ))}
            </select>
          </label>
          {error ? <p className="text-sm text-red-400">{error}</p> : null}
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={handleClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" disabled={loading || activePlans.length === 0}>
              {loading ? t('common.loading') : t('tenants.assignPlan')}
            </Button>
          </div>
        </form>
      ) : null}
    </Modal>
  )
}
