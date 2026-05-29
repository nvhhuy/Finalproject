import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Modal } from '@/shared/components/ui/Modal'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'

export function TenantFormDialog({
  open,
  onClose,
  onSubmit,
}: {
  open: boolean
  onClose: () => void
  onSubmit: (values: {
    slug: string
    displayName: string
    ownerUserId: string
    publicTagline: string
    publicDescription: string
  }) => Promise<void>
}) {
  const { t } = useTranslation()
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setError(null)
    setLoading(true)
    const form = new FormData(e.currentTarget)
    try {
      await onSubmit({
        slug: String(form.get('slug') ?? '').trim(),
        displayName: String(form.get('displayName') ?? '').trim(),
        ownerUserId: String(form.get('ownerUserId') ?? '').trim(),
        publicTagline: String(form.get('publicTagline') ?? '').trim(),
        publicDescription: String(form.get('publicDescription') ?? '').trim(),
      })
      onClose()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('errors.saveFailed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={t('tenants.createTitle')}>
      <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
        <Input
          name="slug"
          label={t('tenants.slug')}
          placeholder={t('tenants.slugPlaceholder')}
          required
          maxLength={63}
        />
        <p className="-mt-2 text-xs text-slate-500">{t('tenants.slugHint')}</p>
        <Input name="displayName" label={t('tenants.displayName')} required maxLength={200} />
        <Input
          name="ownerUserId"
          label={t('tenants.ownerUserId')}
          placeholder={t('tenants.ownerUserIdPlaceholder')}
          required
        />
        <p className="-mt-2 text-xs text-slate-500">{t('tenants.ownerHint')}</p>
        <Input name="publicTagline" label={t('tenants.tagline')} maxLength={300} />
        <Input name="publicDescription" label={t('tenants.description')} maxLength={4000} />
        {error ? <p className="text-sm text-red-400">{error}</p> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={loading}>
            {loading ? t('common.loading') : t('common.create')}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
