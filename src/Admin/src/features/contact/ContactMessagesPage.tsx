import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { listContactMessages, markContactMessageRead } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { formSelectClass } from '@/shared/constants/styles'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Button } from '@/shared/components/ui/Button'
import { Badge } from '@/shared/components/ui/Badge'
import { Input } from '@/shared/components/ui/Input'
import { DataTablePagination } from '@/shared/components/patterns/DataTablePagination'
import { formatDateOrDash, toast, toastApiError } from '@/shared/lib'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { usePagination, useTotalPages } from '@/shared/hooks/usePagination'
import type { ContactMessageSummaryDto } from '@/shared/types/api'

export function ContactMessagesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { page, setPage, resetPage } = usePagination()
  const [searchInput, setSearchInput] = useState('')
  const [isReadFilter, setIsReadFilter] = useState<'' | 'true' | 'false'>('')
  const [selected, setSelected] = useState<ContactMessageSummaryDto | null>(null)
  const debouncedSearch = useDebouncedValue(searchInput.trim(), 350)

  const params = useMemo(
    () => ({
      page,
      pageSize: 20,
      search: debouncedSearch || undefined,
      isRead: isReadFilter === '' ? undefined : isReadFilter === 'true',
    }),
    [page, debouncedSearch, isReadFilter],
  )

  const listQuery = useQuery({
    queryKey: queryKeys.contactMessages.list(params),
    queryFn: () => listContactMessages(params),
  })

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: queryKeys.contactMessages.all() })
  }

  const markReadMutation = useMutation({
    mutationFn: (messageId: string) => markContactMessageRead(messageId),
    onSuccess: async () => {
      await invalidate()
      if (selected) {
        setSelected({ ...selected, isRead: true })
      }
      toast.success(t('contact.markedRead'))
    },
    onError: (err) => toastApiError(err, t('errors.requestFailed')),
  })

  const totalPages = useTotalPages(listQuery.data?.totalCount, listQuery.data?.pageSize ?? 20)

  return (
    <>
      <PageHeader title={t('contact.title')} description={t('contact.description')} />
      <PageContent className="grid gap-4 lg:grid-cols-[1.4fr_1fr]">
        <Card>
          <CardBody className="space-y-4">
            <div className="flex gap-2">
              <Input
                placeholder={t('contact.searchPlaceholder')}
                value={searchInput}
                onChange={(e) => {
                  setSearchInput(e.target.value)
                  resetPage()
                }}
              />
              <select
                className={formSelectClass}
                value={isReadFilter}
                onChange={(e) => {
                  setIsReadFilter(e.target.value as '' | 'true' | 'false')
                  resetPage()
                }}
              >
                <option value="">{t('contact.allMessages')}</option>
                <option value="false">{t('contact.unread')}</option>
                <option value="true">{t('contact.read')}</option>
              </select>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-sm">
                <thead className="border-b border-[var(--color-border)] text-slate-400">
                  <tr>
                    <th className="px-3 py-2 text-left">{t('contact.colSender')}</th>
                    <th className="px-3 py-2 text-left">{t('contact.colSubject')}</th>
                    <th className="px-3 py-2 text-left">{t('contact.colStatus')}</th>
                    <th className="px-3 py-2 text-left">{t('contact.colDate')}</th>
                  </tr>
                </thead>
                <tbody>
                  {(listQuery.data?.items ?? []).map((msg) => (
                    <tr
                      key={msg.id}
                      className={`cursor-pointer border-b border-[var(--color-border)]/40 transition-colors ${selected?.id === msg.id ? 'bg-[var(--color-accent)]/10 outline outline-1 outline-[var(--color-accent)]/30' : 'hover:bg-[var(--color-surface-muted)]/40'}`}
                      onClick={() => setSelected(msg)}
                    >
                      <td className="px-3 py-2">
                        <p className="font-medium text-slate-100">{msg.name}</p>
                        <p className="text-xs text-slate-400">{msg.email}</p>
                      </td>
                      <td className="px-3 py-2 text-slate-300">{msg.subject}</td>
                      <td className="px-3 py-2">
                        {msg.isRead ? (
                          <Badge>{t('contact.read')}</Badge>
                        ) : (
                          <Badge tone="info">{t('contact.unread')}</Badge>
                        )}
                      </td>
                      <td className="px-3 py-2 text-slate-400">{formatDateOrDash(msg.submittedAtUtc)}</td>
                    </tr>
                  ))}
                  {listQuery.data?.items.length === 0 && (
                    <tr>
                      <td colSpan={4} className="px-3 py-6 text-center text-slate-400">
                        {t('contact.empty')}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <DataTablePagination page={page} totalPages={totalPages} onPageChange={setPage} />
          </CardBody>
        </Card>

        <Card>
          <CardBody className="space-y-3">
            {selected ? (
              <>
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-slate-100">{selected.name}</p>
                    <p className="text-xs text-slate-400">{selected.email}</p>
                  </div>
                  {selected.isRead ? (
                    <Badge>{t('contact.read')}</Badge>
                  ) : (
                    <Badge tone="info">{t('contact.unread')}</Badge>
                  )}
                </div>

                <p className="text-sm font-medium text-slate-200">{selected.subject}</p>
                <p className="text-xs text-slate-500">{formatDateOrDash(selected.submittedAtUtc)}</p>

                <div className="min-h-32 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] px-3 py-2 text-sm text-slate-300 whitespace-pre-wrap">
                  {selected.body}
                </div>

                {!selected.isRead && (
                  <Button
                    onClick={() => void markReadMutation.mutateAsync(selected.id)}
                    disabled={markReadMutation.isPending}
                  >
                    {t('contact.markRead')}
                  </Button>
                )}
              </>
            ) : (
              <p className="text-sm text-slate-400">{t('contact.selectMessage')}</p>
            )}
          </CardBody>
        </Card>
      </PageContent>
    </>
  )
}
