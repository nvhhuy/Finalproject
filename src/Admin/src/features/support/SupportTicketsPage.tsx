import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  addSupportTicketMessage,
  assignSupportTicket,
  changeSupportTicketPriority,
  changeSupportTicketStatus,
  getSupportTicketById,
  listSupportTickets,
} from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { formSelectClass } from '@/shared/constants/styles'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Button } from '@/shared/components/ui/Button'
import { Badge } from '@/shared/components/ui/Badge'
import { Input } from '@/shared/components/ui/Input'
import { DataTablePagination } from '@/shared/components/patterns/DataTablePagination'
import { formatDateOrDash, toast } from '@/shared/lib'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { usePagination, useTotalPages } from '@/shared/hooks/usePagination'
import type { SupportTicketPriority, SupportTicketStatus } from '@/shared/types/api'

const STATUS_OPTIONS: SupportTicketStatus[] = ['Open', 'PendingSupport', 'PendingUser', 'Resolved', 'Closed']
const PRIORITY_OPTIONS: SupportTicketPriority[] = ['Low', 'Normal', 'High', 'Urgent']

export function SupportTicketsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { page, setPage, resetPage } = usePagination()
  const [searchInput, setSearchInput] = useState('')
  const [status, setStatus] = useState<SupportTicketStatus | ''>('')
  const [priority, setPriority] = useState<SupportTicketPriority | ''>('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const [internalNote, setInternalNote] = useState(false)
  const debouncedSearch = useDebouncedValue(searchInput.trim(), 350)

  const params = useMemo(
    () => ({
      page,
      pageSize: 20,
      search: debouncedSearch || undefined,
      status: status || undefined,
      priority: priority || undefined,
    }),
    [page, debouncedSearch, status, priority],
  )

  const listQuery = useQuery({
    queryKey: queryKeys.support.list(params),
    queryFn: () => listSupportTickets(params),
  })

  const detailQuery = useQuery({
    queryKey: queryKeys.support.detail(selectedId),
    queryFn: () => getSupportTicketById(selectedId!),
    enabled: Boolean(selectedId),
  })

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.support.all() })
  }

  const statusMutation = useMutation({
    mutationFn: ({ ticketId, value }: { ticketId: string; value: SupportTicketStatus }) =>
      changeSupportTicketStatus(ticketId, value),
    onSuccess: () => void invalidate(),
  })

  const priorityMutation = useMutation({
    mutationFn: ({ ticketId, value }: { ticketId: string; value: SupportTicketPriority }) =>
      changeSupportTicketPriority(ticketId, value),
    onSuccess: () => void invalidate(),
  })

  const assignMutation = useMutation({
    mutationFn: ({ ticketId }: { ticketId: string }) => assignSupportTicket(ticketId, null),
    onSuccess: () => void invalidate(),
  })

  const replyMutation = useMutation({
    mutationFn: ({ ticketId, body, isInternalNote }: { ticketId: string; body: string; isInternalNote: boolean }) =>
      addSupportTicketMessage(ticketId, { body, isInternalNote }),
    onSuccess: () => void invalidate(),
  })

  const totalPages = useTotalPages(listQuery.data?.totalCount, listQuery.data?.pageSize ?? 20)

  return (
    <>
      <PageHeader title={t('support.title')} description={t('support.description')} />
      <PageContent className="grid gap-4 lg:grid-cols-[1.3fr_1fr]">
        <Card>
          <CardBody className="space-y-4">
            <div className="flex gap-2">
              <Input
                placeholder={t('support.searchPlaceholder')}
                value={searchInput}
                onChange={(e) => {
                  setSearchInput(e.target.value)
                  resetPage()
                }}
              />
              <select className={formSelectClass} value={status} onChange={(e) => setStatus(e.target.value as SupportTicketStatus | '')}>
                <option value="">{t('support.allStatuses')}</option>
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>{s}</option>
                ))}
              </select>
              <select className={formSelectClass} value={priority} onChange={(e) => setPriority(e.target.value as SupportTicketPriority | '')}>
                <option value="">{t('support.allPriorities')}</option>
                {PRIORITY_OPTIONS.map((p) => (
                  <option key={p} value={p}>{p}</option>
                ))}
              </select>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="border-b border-[var(--color-border)] text-slate-400">
                  <tr>
                    <th className="px-3 py-2 text-left">{t('support.ticket')}</th>
                    <th className="px-3 py-2 text-left">{t('support.status')}</th>
                    <th className="px-3 py-2 text-left">{t('support.priority')}</th>
                    <th className="px-3 py-2 text-left">{t('support.updated')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(listQuery.data?.items ?? []).map((it) => (
                    <tr
                      key={it.id}
                      className="cursor-pointer border-b border-[var(--color-border)]/40 hover:bg-[var(--color-surface-muted)]/40"
                      onClick={() => setSelectedId(it.id)}
                    >
                      <td className="px-3 py-2">
                        <p className="font-medium text-slate-100">{it.ticketNumber}</p>
                        <p className="text-xs text-slate-400">{it.subject}</p>
                      </td>
                      <td className="px-3 py-2"><Badge>{it.status}</Badge></td>
                      <td className="px-3 py-2"><Badge tone={it.priority === 'Urgent' ? 'danger' : 'default'}>{it.priority}</Badge></td>
                      <td className="px-3 py-2 text-slate-400">{formatDateOrDash(it.updatedAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <DataTablePagination page={page} totalPages={totalPages} onPageChange={setPage} />
          </CardBody>
        </Card>

        <Card>
          <CardBody className="space-y-3">
            {detailQuery.data ? (
              <>
                <p className="text-sm font-semibold text-slate-100">{detailQuery.data.ticketNumber}</p>
                <p className="text-sm text-slate-300">{detailQuery.data.subject}</p>
                <div className="grid grid-cols-2 gap-2">
                  <select
                    className={formSelectClass}
                    value={detailQuery.data.status}
                    onChange={(e) => {
                      if (!selectedId) return
                      void statusMutation.mutateAsync({ ticketId: selectedId, value: e.target.value as SupportTicketStatus })
                    }}
                  >
                    {STATUS_OPTIONS.map((s) => <option key={s} value={s}>{s}</option>)}
                  </select>
                  <select
                    className={formSelectClass}
                    value={detailQuery.data.priority}
                    onChange={(e) => {
                      if (!selectedId) return
                      void priorityMutation.mutateAsync({ ticketId: selectedId, value: e.target.value as SupportTicketPriority })
                    }}
                  >
                    {PRIORITY_OPTIONS.map((p) => <option key={p} value={p}>{p}</option>)}
                  </select>
                </div>
                <Button
                  variant="secondary"
                  onClick={() => {
                    if (!selectedId) return
                    void assignMutation.mutateAsync({ ticketId: selectedId })
                    toast.success(t('support.unassigned'))
                  }}
                >
                  {t('support.unassign')}
                </Button>

                <div className="max-h-64 space-y-2 overflow-auto rounded-lg border border-[var(--color-border)] p-2">
                  {detailQuery.data.messages.map((m) => (
                    <div key={m.id} className="rounded border border-[var(--color-border)] px-2 py-1.5">
                      <p className="text-xs text-slate-500">{formatDateOrDash(m.createdAtUtc)} {m.isInternalNote ? `· ${t('support.internal')}` : ''}</p>
                      <p className="text-sm text-slate-200">{m.body}</p>
                    </div>
                  ))}
                </div>
                <textarea
                  className="min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] px-3 py-2 text-sm"
                  placeholder={t('support.replyPlaceholder')}
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                />
                <label className="flex items-center gap-2 text-xs text-slate-400">
                  <input type="checkbox" checked={internalNote} onChange={(e) => setInternalNote(e.target.checked)} />
                  {t('support.addInternalNote')}
                </label>
                <Button
                  onClick={async () => {
                    if (!selectedId || !message.trim()) return
                    await replyMutation.mutateAsync({ ticketId: selectedId, body: message.trim(), isInternalNote: internalNote })
                    setMessage('')
                    setInternalNote(false)
                    toast.success(t('support.replied'))
                  }}
                >
                  {t('support.reply')}
                </Button>
              </>
            ) : (
              <p className="text-sm text-slate-400">{t('support.selectTicket')}</p>
            )}
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
