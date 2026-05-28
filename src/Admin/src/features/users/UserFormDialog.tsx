import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  createUserFormSchemas,
  firstZodError,
  getApiErrorMessage,
} from '@/shared/lib'
import { formSelectClass } from '@/shared/constants/styles'
import { cn } from '@/shared/lib/cn'
import { Modal } from '@/shared/components/ui/Modal'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import { USER_ROLES, type AdminUserDetailDto } from '@/shared/types/api'

interface UserFormDialogProps {
  open: boolean
  onClose: () => void
  mode: 'create' | 'edit'
  detail?: AdminUserDetailDto
  detailLoading?: boolean
  onSubmit: (values: {
    firstName: string
    lastName: string
    userName: string
    email: string
    role: (typeof USER_ROLES)[number]
    password: string
  }) => Promise<void>
}

export function UserFormDialog({
  open,
  onClose,
  mode,
  detail,
  detailLoading,
  onSubmit,
}: UserFormDialogProps) {
  const { t } = useTranslation()
  const schemas = useMemo(() => createUserFormSchemas(t), [t])
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const seed = mode === 'edit' && detail
    ? {
        firstName: detail.firstName,
        lastName: detail.lastName,
        userName: detail.userName,
        email: detail.email,
        role: detail.role,
      }
    : {
        firstName: '',
        lastName: '',
        userName: '',
        email: '',
        role: 'User',
      }
  const formKey = `${mode}-${detail?.id ?? 'new'}-${open ? 'open' : 'closed'}`

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (mode === 'edit' && detailLoading) return
    const form = e.currentTarget as HTMLFormElement
    const formData = new FormData(form)

    const values = {
      firstName: String(formData.get('firstName') ?? '').trim(),
      lastName: String(formData.get('lastName') ?? '').trim(),
      userName: String(formData.get('userName') ?? '').trim(),
      email: String(formData.get('email') ?? '').trim(),
      role: String(formData.get('role') ?? 'User') as (typeof USER_ROLES)[number],
      password: String(formData.get('password') ?? ''),
    }
    const schema = mode === 'create' ? schemas.create : schemas.base
    const parsed = schema.safeParse(values)
    const zodErr = firstZodError(parsed, t)
    if (zodErr) {
      setError(zodErr)
      return
    }

    setLoading(true)
    setError(null)
    try {
      await onSubmit(values)
      onClose()
    } catch (err) {
      setError(getApiErrorMessage(err, 'errors.saveFailed'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title={mode === 'create' ? t('users.createDialogTitle') : t('users.editDialogTitle')}
    >
      {mode === 'edit' && detailLoading ? (
        <p className="text-sm text-slate-400">{t('users.loadingUser')}</p>
      ) : (
        <form key={formKey} onSubmit={handleSubmit} className="space-y-4">
          {error ? <p className="text-sm text-red-400">{error}</p> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Input
              label={t('users.firstName')}
              name="firstName"
              defaultValue={seed.firstName}
              required
            />
            <Input
              label={t('users.lastName')}
              name="lastName"
              defaultValue={seed.lastName}
              required
            />
          </div>
          <Input
            label={t('users.username')}
            name="userName"
            defaultValue={seed.userName}
            required
          />
          <Input
            label={t('login.email')}
            type="email"
            name="email"
            defaultValue={seed.email}
            required
          />
          <div className="space-y-1.5">
            <label htmlFor="role" className="block text-sm font-medium text-slate-300">
              {t('users.role')}
            </label>
            <select
              id="role"
              name="role"
              defaultValue={seed.role}
              className={cn(formSelectClass, 'w-full')}
            >
              {USER_ROLES.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>
          </div>
          {mode === 'create' ? (
            <Input
              label={t('users.password')}
              type="password"
              name="password"
              required
              autoComplete="new-password"
            />
          ) : null}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={onClose}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={loading} disabled={mode === 'edit' && detailLoading}>
              {mode === 'create' ? t('common.create') : t('common.save')}
            </Button>
          </div>
        </form>
      )}
    </Modal>
  )
}
