import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PLAN_CURRENCIES } from '@/shared/constants/plan-currencies'
import { formSelectClass } from '@/shared/constants/styles'
import { Modal } from '@/shared/components/ui/Modal'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import type { CreditPackageDto } from '@/shared/types/api'

export function CreditPackageFormDialog({
  open,
  onClose,
  mode,
  pkg,
  onSubmit,
}: {
  open: boolean
  onClose: () => void
  mode: 'create' | 'edit'
  pkg?: CreditPackageDto
  onSubmit: (values: {
    code: string
    name: string
    description: string
    priceCents: number
    currency: string
    creditsAmount: number
    sortOrder: number
    isActive: boolean
  }) => Promise<void>
}) {
  const { t } = useTranslation()
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const formKey = `${mode}-${pkg?.id ?? 'new'}-${open ? 'open' : 'closed'}`

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
        priceCents: Number(fd.get('priceCents') ?? 0),
        currency: String(fd.get('currency') ?? 'USD'),
        creditsAmount: Number(fd.get('creditsAmount') ?? 0),
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
    <Modal open={open} onClose={onClose} title={mode === 'create' ? t('creditPackages.create') : t('creditPackages.edit')}>
      <form key={formKey} onSubmit={(e) => void handleSubmit(e)} className="space-y-3">
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        <div>
          <label className="text-sm font-medium">{t('creditPackages.code')}</label>
          <Input name="code" defaultValue={pkg?.code ?? ''} disabled={mode === 'edit'} required />
        </div>
        <div>
          <label className="text-sm font-medium">{t('creditPackages.name')}</label>
          <Input name="name" defaultValue={pkg?.name ?? ''} required />
        </div>
        <div>
          <label className="text-sm font-medium">{t('creditPackages.description')}</label>
          <Input name="description" defaultValue={pkg?.description ?? ''} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-sm font-medium">{t('creditPackages.priceCents')}</label>
            <Input name="priceCents" type="number" min={0} defaultValue={pkg?.priceCents ?? 499} required />
          </div>
          <div>
            <label className="text-sm font-medium">{t('creditPackages.currency')}</label>
            <select
              name="currency"
              className={formSelectClass}
              defaultValue={pkg?.currency ?? 'USD'}
            >
              {PLAN_CURRENCIES.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="text-sm font-medium">{t('creditPackages.creditsAmount')}</label>
            <Input name="creditsAmount" type="number" min={1} defaultValue={pkg?.creditsAmount ?? 500} required />
          </div>
          <div>
            <label className="text-sm font-medium">{t('creditPackages.sortOrder')}</label>
            <Input name="sortOrder" type="number" defaultValue={pkg?.sortOrder ?? 10} required />
          </div>
        </div>
        {mode === 'edit' ? (
          <label className="flex items-center gap-2 text-sm">
            <input name="isActive" type="checkbox" defaultChecked={pkg?.isActive ?? true} />
            {t('creditPackages.active')}
          </label>
        ) : null}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={loading}>
            {t('common.save')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
