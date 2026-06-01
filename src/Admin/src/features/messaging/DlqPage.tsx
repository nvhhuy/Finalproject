import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { drainDlq, peekDlq, replayDlq } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { DEFAULT_DLQ_QUEUE } from '@/shared/types/api'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { toast, toastApiError, confirmAction, formatEmpty } from '@/shared/lib'
import { Card, CardBody, CardHeader } from '@/shared/components/ui/Card'
import { Input } from '@/shared/components/ui/Input'
import { Button } from '@/shared/components/ui/Button'
import { Alert } from '@/shared/components/ui/Alert'
import { ApiClientError } from '@/shared/lib/errors'

export function DlqPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [queueName, setQueueName] = useState(DEFAULT_DLQ_QUEUE)
  const [count, setCount] = useState(10)
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: queryKeys.dlq.peek(queueName, count),
    queryFn: () => peekDlq(queueName, count),
    enabled: false,
  })

  const replayMutation = useMutation({
    mutationFn: () => replayDlq(queueName, count),
    onSuccess: async (result) => {
      toast.success(
        t('toast.replayedMessages', { count: result.count, queue: result.queue }),
      )
      await queryClient.invalidateQueries({ queryKey: queryKeys.dlq.all() })
      // Re-peek so the panel reflects current queue state after replay
      await refetch()
    },
    onError: (err) => toastApiError(err, 'toast.replayFailed'),
  })

  const drainMutation = useMutation({
    mutationFn: () => drainDlq(queueName, count),
    onSuccess: async (result) => {
      toast.success(
        t('toast.drainedMessages', { count: result.count, queue: result.queue }),
      )
      await queryClient.invalidateQueries({ queryKey: queryKeys.dlq.all() })
      // Re-peek so the panel clears after drain
      await refetch()
    },
    onError: (err) => toastApiError(err, 'toast.drainFailed'),
  })

  return (
    <>
      <PageHeader title={t('dlq.title')} description={t('dlq.description')} />
      <PageContent>
        {error ? (
          <Alert variant="error">
            {error instanceof ApiClientError ? error.message : t('dlq.peekFailed')}
          </Alert>
        ) : null}

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">{t('dlq.queueControls')}</h2>
          </CardHeader>
          <CardBody className="flex flex-wrap items-end gap-4">
            <Input
              label={t('dlq.queueName')}
              value={queueName}
              onChange={(e) => setQueueName(e.target.value)}
              className="min-w-[240px] flex-1"
            />
            <Input
              label={t('dlq.batchSize')}
              type="number"
              min={1}
              max={100}
              value={String(count)}
              onChange={(e) => setCount(Number(e.target.value) || 10)}
              className="w-28"
            />
            <Button variant="secondary" onClick={() => void refetch()} loading={isLoading}>
              {t('dlq.peek')}
            </Button>
            <Button
              variant="secondary"
              loading={replayMutation.isPending}
              onClick={() => {
                if (confirmAction(t('dlq.replayConfirm'))) {
                  replayMutation.mutate()
                }
              }}
            >
              {t('dlq.replay')}
            </Button>
            <Button
              variant="danger"
              loading={drainMutation.isPending}
              onClick={() => {
                if (confirmAction(t('dlq.drainConfirm'))) {
                  drainMutation.mutate()
                }
              }}
            >
              {t('dlq.drain')}
            </Button>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>
            <h2 className="font-medium text-white">
              {data
                ? t('dlq.messagesCount', { count: data.length })
                : t('dlq.messages')}
            </h2>
          </CardHeader>
          <CardBody className="space-y-4">
            {data?.length ? (
              data.map((msg) => (
                <div
                  key={msg.deliveryTag}
                  className="rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)]/50 p-4 text-sm"
                >
                  <div className="flex flex-wrap gap-4 text-xs text-slate-400">
                    <span>
                      {t('dlq.tag')}: {msg.deliveryTag}
                    </span>
                    <span>
                      {t('dlq.retries')}: {msg.retryCount}
                    </span>
                    <span>
                      {t('dlq.reason')}: {formatEmpty(msg.lastFailureReason ?? undefined)}
                    </span>
                    <span>
                      {t('dlq.routing')}: {formatEmpty(msg.originalRoutingKey ?? undefined)}
                    </span>
                  </div>
                  <pre className="mt-2 max-h-32 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-200">
                    {msg.payloadPreview}
                  </pre>
                </div>
              ))
            ) : (
              <p className="text-sm text-slate-400">{t('dlq.emptyHint')}</p>
            )}
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
