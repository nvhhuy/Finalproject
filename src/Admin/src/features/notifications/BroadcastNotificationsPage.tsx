import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { broadcastMemberNotification } from '@/shared/api/admin-api'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import { Alert } from '@/shared/components/ui/Alert'
import { confirmAction, toast, toastApiError } from '@/shared/lib'

export function BroadcastNotificationsPage() {
  const { t } = useTranslation()
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [linkUrl, setLinkUrl] = useState('')

  const broadcastMutation = useMutation({
    mutationFn: () =>
      broadcastMemberNotification({
        title: title.trim(),
        body: body.trim(),
        linkUrl: linkUrl.trim() || null,
      }),
    onSuccess: (result) => {
      toast.success(t('broadcastNotifications.sent', { count: result.recipientCount }))
      setTitle('')
      setBody('')
      setLinkUrl('')
    },
    onError: (err) => toastApiError(err, 'broadcastNotifications.sendFailed'),
  })

  const canSubmit = title.trim().length > 0 && body.trim().length > 0

  const handleSubmit = () => {
    if (!canSubmit) return
    if (!confirmAction(t('broadcastNotifications.confirm'))) return
    broadcastMutation.mutate()
  }

  return (
    <>
      <PageHeader
        title={t('broadcastNotifications.title')}
        description={t('broadcastNotifications.description')}
      />
      <PageContent>
        <Alert variant="info">{t('broadcastNotifications.hint')}</Alert>

        <Card className="mt-4">
          <CardHeader>
            <h2 className="font-medium text-white">{t('broadcastNotifications.formTitle')}</h2>
          </CardHeader>
          <CardBody className="space-y-4">
            <Input
              label={t('broadcastNotifications.titleLabel')}
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              maxLength={200}
              placeholder={t('broadcastNotifications.titlePlaceholder')}
            />
            <label className="block space-y-1.5">
              <span className="text-sm font-medium text-slate-300">
                {t('broadcastNotifications.bodyLabel')}
              </span>
              <textarea
                value={body}
                onChange={(e) => setBody(e.target.value)}
                maxLength={2000}
                rows={6}
                placeholder={t('broadcastNotifications.bodyPlaceholder')}
                className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:border-[var(--color-accent)] focus:outline-none focus:ring-1 focus:ring-[var(--color-accent)]"
              />
            </label>
            <Input
              label={t('broadcastNotifications.linkLabel')}
              value={linkUrl}
              onChange={(e) => setLinkUrl(e.target.value)}
              maxLength={500}
              placeholder={t('broadcastNotifications.linkPlaceholder')}
            />
            <div className="flex justify-end">
              <Button
                onClick={handleSubmit}
                loading={broadcastMutation.isPending}
                disabled={!canSubmit}
              >
                {t('broadcastNotifications.send')}
              </Button>
            </div>
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
