import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { exportAuditLogsCsv, listAuditLogs } from '@/shared/api/admin-api'
import { Button } from '@/shared/components/ui/Button'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { DataTablePagination } from '@/shared/components/patterns/DataTablePagination'
import { Card } from '@/shared/components/ui/Card'
import { Input } from '@/shared/components/ui/Input'
import { usePagination, useTotalPages } from '@/shared/hooks/usePagination'
import { formatDateTimeOrDash, formatEmpty } from '@/shared/lib'

const PAGE_SIZE = 50

export function AuditLogsPage() {
  const { t } = useTranslation()
  const [searchParams] = useSearchParams()
  const initialTarget = searchParams.get('targetUserId') ?? ''
  const { page, setPage, resetPage } = usePagination()
  const [targetUserId, setTargetUserId] = useState(initialTarget)

  const { data, isLoading } = useQuery({
    queryKey: queryKeys.auditLogs.list(page, targetUserId),
    queryFn: () =>
      listAuditLogs({
        page,
        pageSize: PAGE_SIZE,
        targetUserId: targetUserId || undefined,
      }),
  })

  const totalPages = useTotalPages(data?.totalCount, data?.pageSize ?? PAGE_SIZE)

  async function handleExport() {
    const blob = await exportAuditLogsCsv({
      targetUserId: targetUserId || undefined,
    })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `admin-audit-${new Date().toISOString().slice(0, 10)}.csv`
    anchor.click()
    URL.revokeObjectURL(url)
  }

  return (
    <>
      <PageHeader
        title={t('audit.title')}
        description={t('audit.description')}
        actions={
          <Button variant="secondary" onClick={() => void handleExport()}>
            {t('audit.exportCsv')}
          </Button>
        }
      />
      <PageContent>
        <Card className="p-4">
          <div className="flex flex-wrap gap-3">
            <Input
              label={t('audit.filterLabel')}
              value={targetUserId}
              onChange={(e) => {
                setTargetUserId(e.target.value)
                resetPage()
              }}
              placeholder={t('audit.filterPlaceholder')}
              className="min-w-[280px] flex-1"
            />
          </div>
        </Card>

        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--color-border)] text-slate-400">
                <tr>
                  <th className="px-4 py-3">{t('audit.colTime')}</th>
                  <th className="px-4 py-3">{t('audit.colAction')}</th>
                  <th className="px-4 py-3">{t('audit.colActor')}</th>
                  <th className="px-4 py-3">{t('audit.colTarget')}</th>
                  <th className="px-4 py-3">{t('audit.colDetails')}</th>
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                      {t('common.loading')}
                    </td>
                  </tr>
                ) : data?.items.length ? (
                  data.items.map((row) => (
                    <tr
                      key={row.id}
                      className="border-b border-[var(--color-border)]/50 text-slate-300"
                    >
                      <td className="whitespace-nowrap px-4 py-2 text-xs text-slate-400">
                        {formatDateTimeOrDash(row.createdAtUtc)}
                      </td>
                      <td className="px-4 py-2 font-mono text-xs text-blue-300">{row.action}</td>
                      <td className="max-w-[120px] truncate px-4 py-2 font-mono text-xs">
                        {row.actorUserId}
                      </td>
                      <td className="max-w-[120px] truncate px-4 py-2 font-mono text-xs">
                        {formatEmpty(row.targetUserId ?? undefined)}
                      </td>
                      <td className="max-w-md truncate px-4 py-2 text-xs text-slate-400">
                        {formatEmpty(row.details ?? row.ipAddress ?? undefined)}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                      {t('audit.noEntries')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <DataTablePagination page={page} totalPages={totalPages} onPageChange={setPage} />
        </Card>
      </PageContent>
    </>
  )
}
