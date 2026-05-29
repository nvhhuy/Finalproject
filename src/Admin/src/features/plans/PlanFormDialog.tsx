import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formSelectClass } from '@/shared/constants/styles'
import { PLAN_CURRENCIES } from '@/shared/constants/plan-currencies'
import { cn } from '@/shared/lib/cn'
import { Modal } from '@/shared/components/ui/Modal'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import type { PlanDto } from '@/shared/types/api'

function FieldHint({ children }: { children: string }) {
  return <p className="text-xs text-slate-500">{children}</p>
}

export function PlanFormDialog({
  open,
  onClose,
  mode,
  plan,
  onSubmit,
}: {
  open: boolean
  onClose: () => void
  mode: 'create' | 'edit'
  plan?: PlanDto
  onSubmit: (values: {
    code: string
    name: string
    description: string
    priceMonthlyCents: number
    currency: string
    trialDays: number
    maxUsers: number
    sortOrder: number
    isActive: boolean
  }) => Promise<void>
}) {
  const { t } = useTranslation()
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const seed =
    mode === 'edit' && plan
      ? {
          code: plan.code,
          name: plan.name,
          description: plan.description ?? '',
          priceMonthlyCents: plan.priceMonthlyCents,
          currency: PLAN_CURRENCIES.includes(plan.currency as (typeof PLAN_CURRENCIES)[number])
            ? plan.currency
            : 'USD',
          trialDays: plan.trialDays,
          maxUsers: plan.limits.maxUsers,
          sortOrder: plan.sortOrder,
          isActive: plan.isActive,
        }
      : {
          code: '',
          name: '',
          description: '',
          priceMonthlyCents: 2900,
          currency: 'USD',
          trialDays: 14,
          maxUsers: 5,
          sortOrder: 10,
          isActive: true,
        }

  const formKey = `${mode}-${plan?.id ?? 'new'}-${open ? 'open' : 'closed'}`

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    const form = e.currentTarget as HTMLFormElement
    const fd = new FormData(form)
    const code = String(fd.get('code') ?? '').trim()
    const name = String(fd.get('name') ?? '').trim()
    if (!code || !name) {
      setError(t('validation.required'))
      return
    }
    setLoading(true)
    try {
      await onSubmit({
        code,
        name,
        description: String(fd.get('description') ?? '').trim(),
        priceMonthlyCents: Number(fd.get('priceMonthlyCents') ?? 0),
        currency: String(fd.get('currency') ?? 'USD'),
        trialDays: Number(fd.get('trialDays') ?? 0),
        maxUsers: Number(fd.get('maxUsers') ?? 5),
        sortOrder: Number(fd.get('sortOrder') ?? 10),
        isActive: fd.get('isActive') === 'on',
      })
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('errors.requestFailed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={mode === 'create' ? t('plans.createTitle') : t('plans.editTitle')}
    >
      <form key={formKey} className="space-y-4" onSubmit={handleSubmit}>
        {error ? <p className="text-sm text-red-400">{error}</p> : null}

        <div className="space-y-1">
          <Input
            label={t('plans.code')}
            name="code"
            placeholder={t('plans.codePlaceholder')}
            required
            defaultValue={seed.code}
            readOnly={mode === 'edit'}
            disabled={mode === 'edit'}
          />
          {mode === 'create' ? <FieldHint>{t('plans.codeHint')}</FieldHint> : null}
        </div>

        <Input
          label={t('plans.name')}
          name="name"
          placeholder={t('plans.namePlaceholder')}
          required
          defaultValue={seed.name}
        />

        <div className="space-y-1">
          <Input
            label={t('plans.description')}
            name="description"
            placeholder={t('plans.descriptionPlaceholder')}
            defaultValue={seed.description}
          />
          <FieldHint>{t('plans.descriptionHint')}</FieldHint>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1">
            <Input
              label={t('plans.priceCents')}
              name="priceMonthlyCents"
              type="number"
              min={0}
              placeholder="2900"
              defaultValue={seed.priceMonthlyCents}
            />
            <FieldHint>{t('plans.priceCentsHint')}</FieldHint>
          </div>
          <div className="space-y-1.5">
            <label htmlFor="plan-currency" className="block text-sm font-medium text-slate-300">
              {t('plans.currency')}
            </label>
            <select
              id="plan-currency"
              name="currency"
              className={cn(formSelectClass, 'w-full')}
              defaultValue={seed.currency}
            >
              {PLAN_CURRENCIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            <FieldHint>{t('plans.currencyHint')}</FieldHint>
          </div>
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label={t('plans.trialDaysLabel')}
            name="trialDays"
            type="number"
            min={0}
            placeholder="14"
            defaultValue={seed.trialDays}
          />
          <Input
            label={t('plans.maxUsersLabel')}
            name="maxUsers"
            type="number"
            min={1}
            placeholder="5"
            defaultValue={seed.maxUsers}
          />
        </div>

        <div className="space-y-1">
          <Input
            label={t('plans.sortOrder')}
            name="sortOrder"
            type="number"
            placeholder="10"
            defaultValue={seed.sortOrder}
          />
          <FieldHint>{t('plans.sortOrderHint')}</FieldHint>
        </div>

        {mode === 'edit' ? (
          <label className="flex items-center gap-2 text-sm text-slate-300">
            <input
              type="checkbox"
              name="isActive"
              defaultChecked={seed.isActive}
              className="size-4 rounded border-slate-600 bg-slate-800"
            />
            {t('plans.isActive')}
          </label>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={loading}>
            {loading ? t('common.loading') : mode === 'create' ? t('common.create') : t('common.save')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
