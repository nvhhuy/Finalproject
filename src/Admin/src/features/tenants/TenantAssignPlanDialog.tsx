import { useEffect, useState } from 'react'
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
  const [planCode, setPlanCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const { data: plans } = useQuery({
    queryKey: queryKeys.plans.list(),
    queryFn: () => listPlans(),
    enabled: open,
  })

  const activePlans = (plans ?? []).filter((p) => p.isActive)

  useEffect(() => {
    if (open && tenant) {
      setPlanCode(tenant.subscriptionPlanCode ?? '')
      setError(null)
    }
  }, [open, tenant])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!planCode) return
    setError(null)
    setLoading(true)
    try {
      await onSubmit(planCode)
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('errors.saveFailed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
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
              value={planCode}
              onChange={(e) => setPlanCode(e.target.value)}
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
            <Button type="button" variant="secondary" onClick={onClose}>
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
