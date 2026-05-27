import { useEffect, useMemo, useState } from 'react'
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

  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [userName, setUserName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<string>('User')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  useEffect(() => {
    if (!open) return
    if (mode === 'edit' && detail) {
      setFirstName(detail.firstName)
      setLastName(detail.lastName)
      setUserName(detail.userName)
      setEmail(detail.email)
      setRole(detail.role)
    } else if (mode === 'create') {
      setFirstName('')
      setLastName('')
      setUserName('')
      setEmail('')
      setRole('User')
    }
    setPassword('')
    setError(null)
  }, [open, mode, detail])

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (mode === 'edit' && detailLoading) return

    const values = {
      firstName,
      lastName,
      userName,
      email,
      role: role as (typeof USER_ROLES)[number],
      password,
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
        <form onSubmit={handleSubmit} className="space-y-4">
          {error ? <p className="text-sm text-red-400">{error}</p> : null}
          <div className="grid gap-4 sm:grid-cols-2">
            <Input
              label={t('users.firstName')}
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
              required
            />
            <Input
              label={t('users.lastName')}
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
              required
            />
          </div>
          <Input
            label={t('users.username')}
            value={userName}
            onChange={(e) => setUserName(e.target.value)}
            required
          />
          <Input
            label={t('login.email')}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
          <div className="space-y-1.5">
            <label htmlFor="role" className="block text-sm font-medium text-slate-300">
              {t('users.role')}
            </label>
            <select
              id="role"
              value={role}
              onChange={(e) => setRole(e.target.value)}
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
              value={password}
              onChange={(e) => setPassword(e.target.value)}
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
