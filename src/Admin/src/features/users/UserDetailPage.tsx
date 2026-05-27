import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ArrowLeft, Copy, KeyRound, Mail, Shield, UserX } from 'lucide-react'
import {
  deleteUser,
  getUserById,
  impersonateUser,
  lockUser,
  markEmailVerified,
  resendEmailVerification,
  resetPassword,
  resetTwoFactor,
  revokeSessions,
  unlockUser,
} from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Button } from '@/shared/components/ui/Button'
import { Badge } from '@/shared/components/ui/Badge'
import { Modal } from '@/shared/components/ui/Modal'
import { Input } from '@/shared/components/ui/Input'
import { ApiClientError } from '@/shared/lib/errors'
import {
  copyWithToast,
  confirmAction,
  formatDateTimeOrDash,
  getApiErrorMessage,
  runAdminAction,
  toast,
  toastApiError,
} from '@/shared/lib'
import type { AuthResponseDto } from '@/shared/types/api'

type UserActionKey =
  | 'unlock'
  | 'lock'
  | 'revokeSessions'
  | 'passwordReset'
  | 'reset2fa'
  | 'markVerified'
  | 'resendVerification'
  | 'delete'

export function UserDetailPage() {
  const { t } = useTranslation()
  const { userId } = useParams<{ userId: string }>()
  const queryClient = useQueryClient()
  const [lockReason, setLockReason] = useState('')
  const [lockModalOpen, setLockModalOpen] = useState(false)
  const [passwordModalOpen, setPasswordModalOpen] = useState(false)
  const [newPassword, setNewPassword] = useState('')
  const [impersonation, setImpersonation] = useState<AuthResponseDto | null>(null)

  const { data: user, isLoading, error: loadError } = useQuery({
    queryKey: queryKeys.users.detail(userId),
    queryFn: () => getUserById(userId!),
    enabled: Boolean(userId),
  })

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.users.all() })
    void queryClient.invalidateQueries({ queryKey: queryKeys.auditLogs.all() })
  }

  const rowVersion = () => {
    if (!user) {
      throw new ApiClientError({
        code: 'MissingRowVersion',
        message: t('errors.userNotLoaded'),
        httpStatus: null,
      })
    }
    return user.rowVersion
  }

  const runAction = (actionKey: UserActionKey, fn: () => Promise<unknown>) =>
    runAdminAction(t, `userDetail.actions.${actionKey}`, fn, { onSuccess: invalidate })

  const impersonateMutation = useMutation({
    mutationFn: () => impersonateUser(userId!, rowVersion()),
    onSuccess: (data) => {
      setImpersonation(data)
      toast.success(t('toast.impersonationIssued'))
      invalidate()
    },
    onError: (err) => toastApiError(err, 'toast.impersonationFailed'),
  })

  if (!userId) {
    return <p className="p-4 text-slate-400 sm:p-6">{t('errors.invalidUserId')}</p>
  }

  return (
    <>
      <PageHeader
        title={user?.userName ?? t('userDetail.user')}
        description={
          user?.email ??
          (isLoading ? t('common.loading') : loadError ? t('errors.userNotFound') : '')
        }
        actions={
          <Link to="/users">
            <Button variant="secondary" className="w-full sm:w-auto">
              <ArrowLeft className="size-4" />
              {t('common.back')}
            </Button>
          </Link>
        }
      />
      <PageContent>
        {loadError ? (
          <p className="text-sm text-red-400">
            {getApiErrorMessage(loadError, 'errors.userNotFound')}
          </p>
        ) : null}

        {user ? (
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center gap-2">
                <Badge tone={user.role === 'Admin' ? 'warning' : 'default'}>{user.role}</Badge>
                {user.isLocked ? (
                  <Badge tone="danger">{t('userDetail.locked')}</Badge>
                ) : (
                  <Badge tone="success">{t('userDetail.active')}</Badge>
                )}
                {user.emailVerifiedAtUtc ? (
                  <Badge tone="success">{t('userDetail.emailVerified')}</Badge>
                ) : (
                  <Badge tone="warning">{t('userDetail.emailNotVerified')}</Badge>
                )}
              </div>
            </CardHeader>
            <CardBody className="grid gap-2 text-sm text-slate-300 sm:grid-cols-2">
              <p>
                <span className="text-slate-500">{t('userDetail.name')}:</span>{' '}
                {user.firstName} {user.lastName}
              </p>
              <p className="break-all">
                <span className="text-slate-500">{t('userDetail.id')}:</span>{' '}
                <code className="text-slate-200">{user.id}</code>
              </p>
              <p>
                <span className="text-slate-500">{t('userDetail.twoFa')}:</span>{' '}
                {user.twoFactorEnabled ? t('userDetail.twoFaOn') : t('userDetail.twoFaOff')}
              </p>
              <p>
                <span className="text-slate-500">{t('userDetail.lastLogin')}:</span>{' '}
                {formatDateTimeOrDash(user.lastLoginAtUtc)}
              </p>
              <p>
                <span className="text-slate-500">{t('userDetail.registered')}:</span>{' '}
                {formatDateTimeOrDash(user.registeredAtUtc)}
              </p>
              {user.lockReason ? (
                <p className="sm:col-span-2">
                  <span className="text-slate-500">{t('userDetail.lockReason')}:</span>{' '}
                  {user.lockReason}
                </p>
              ) : null}
            </CardBody>
          </Card>
        ) : null}

        <div className="grid gap-4 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <h2 className="font-medium text-white">{t('userDetail.accountAccess')}</h2>
            </CardHeader>
            <CardBody className="flex flex-wrap gap-2">
              {user?.isLocked ? (
                <Button
                  variant="secondary"
                  className="w-full sm:w-auto"
                  onClick={() => runAction('unlock', () => unlockUser(userId, rowVersion()))}
                >
                  {t('userDetail.unlockAccount')}
                </Button>
              ) : (
                <Button
                  variant="secondary"
                  className="w-full sm:w-auto"
                  onClick={() => setLockModalOpen(true)}
                >
                  <UserX className="size-4" />
                  {t('userDetail.lockAccount')}
                </Button>
              )}
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                onClick={() =>
                  runAction('revokeSessions', () => revokeSessions(userId, rowVersion()))
                }
              >
                {t('userDetail.revokeSessions')}
              </Button>
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                onClick={() => setPasswordModalOpen(true)}
              >
                <KeyRound className="size-4" />
                {t('userDetail.resetPassword')}
              </Button>
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                onClick={() => runAction('reset2fa', () => resetTwoFactor(userId, rowVersion()))}
              >
                <Shield className="size-4" />
                {t('userDetail.reset2fa')}
              </Button>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <h2 className="font-medium text-white">{t('userDetail.emailSection')}</h2>
            </CardHeader>
            <CardBody className="flex flex-wrap gap-2">
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                onClick={() =>
                  runAction('markVerified', () => markEmailVerified(userId, rowVersion()))
                }
              >
                <Mail className="size-4" />
                {t('userDetail.markVerified')}
              </Button>
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                onClick={() =>
                  runAction('resendVerification', () =>
                    resendEmailVerification(userId, rowVersion()),
                  )
                }
              >
                {t('userDetail.resendVerification')}
              </Button>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <h2 className="font-medium text-white">{t('userDetail.supportDebug')}</h2>
            </CardHeader>
            <CardBody className="space-y-3">
              <p className="text-sm text-slate-400">{t('userDetail.impersonationHelp')}</p>
              <Button
                variant="secondary"
                className="w-full sm:w-auto"
                loading={impersonateMutation.isPending}
                onClick={() => impersonateMutation.mutate()}
                disabled={user?.role === 'Admin'}
              >
                {t('userDetail.impersonateUser')}
              </Button>
            </CardBody>
          </Card>

          <Card>
            <CardHeader>
              <h2 className="font-medium text-red-300">{t('userDetail.dangerZone')}</h2>
            </CardHeader>
            <CardBody>
              <Button
                variant="danger"
                className="w-full sm:w-auto"
                onClick={() => {
                  if (confirmAction(t('userDetail.deleteConfirm'))) {
                    void runAction('delete', () => deleteUser(userId, rowVersion()))
                  }
                }}
              >
                {t('userDetail.deleteUser')}
              </Button>
            </CardBody>
          </Card>
        </div>

        <p className="text-sm text-slate-500">
          <Link to={`/audit-logs?targetUserId=${userId}`} className="text-blue-400 hover:underline">
            {t('userDetail.viewAuditLogs')}
          </Link>
        </p>
      </PageContent>

      <Modal
        open={lockModalOpen}
        onClose={() => setLockModalOpen(false)}
        title={t('userDetail.lockModalTitle')}
      >
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault()
            void runAction('lock', () => lockUser(userId, rowVersion(), lockReason)).then(() =>
              setLockModalOpen(false),
            )
          }}
        >
          <Input
            label={t('userDetail.lockReasonOptional')}
            value={lockReason}
            onChange={(e) => setLockReason(e.target.value)}
          />
          <Button type="submit" variant="danger" className="w-full">
            {t('userDetail.lockAccount')}
          </Button>
        </form>
      </Modal>

      <Modal
        open={passwordModalOpen}
        onClose={() => setPasswordModalOpen(false)}
        title={t('userDetail.resetPasswordTitle')}
      >
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault()
            void runAction('passwordReset', () =>
              resetPassword(userId, newPassword, rowVersion()),
            ).then(() => {
              setPasswordModalOpen(false)
              setNewPassword('')
            })
          }}
        >
          <Input
            label={t('userDetail.newPassword')}
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            required
            minLength={8}
          />
          <Button type="submit" className="w-full">
            {t('userDetail.resetPassword')}
          </Button>
        </form>
      </Modal>

      <Modal
        open={impersonation !== null}
        onClose={() => setImpersonation(null)}
        title={t('userDetail.impersonationTitle')}
        className="max-w-2xl"
      >
        {impersonation ? (
          <div className="space-y-3 text-sm">
            <p className="text-amber-300">
              {t('userDetail.impersonationWarning', {
                date: formatDateTimeOrDash(impersonation.accessTokenExpiresUtc),
              })}
            </p>
            <TokenField label={t('userDetail.accessToken')} value={impersonation.accessToken} />
            <TokenField label={t('userDetail.refreshToken')} value={impersonation.refreshToken} />
          </div>
        ) : null}
      </Modal>
    </>
  )
}

function TokenField({ label, value }: { label: string; value: string }) {
  const { t } = useTranslation()

  return (
    <div>
      <p className="mb-1 text-slate-400">{label}</p>
      <div className="flex flex-col gap-2 sm:flex-row">
        <code className="max-h-24 flex-1 overflow-auto rounded bg-black/40 p-2 text-xs text-slate-200">
          {value}
        </code>
        <Button
          size="sm"
          variant="secondary"
          className="shrink-0 sm:self-start"
          onClick={() => void copyWithToast(value, label)}
          aria-label={t('common.copy')}
        >
          <Copy className="size-4" />
          {t('common.copy')}
        </Button>
      </div>
    </div>
  )
}
