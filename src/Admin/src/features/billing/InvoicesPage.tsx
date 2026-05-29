import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { listPlatformInvoices } from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody } from '@/shared/components/ui/Card'
import type { BillingInvoiceDto } from '@/shared/types/api'

function formatAmount(inv: BillingInvoiceDto) {
  if (inv.currency === 'CREDITS') return `${inv.amountCredits ?? 0} credits`
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: inv.currency }).format(
      inv.amountCents / 100,
    )
  } catch {
    return `${(inv.amountCents / 100).toFixed(2)} ${inv.currency}`
  }
}

export function InvoicesPage() {
  const { t } = useTranslation()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.invoices.list(),
    queryFn: () => listPlatformInvoices(),
  })

  const invoices = data ?? []

  return (
    <PageContent>
      <PageHeader title={t('invoices.title')} description={t('invoices.subtitle')} />

      {isLoading ? <p className="text-sm text-slate-400">{t('common.loading')}</p> : null}
      {error ? <p className="text-sm text-red-400">{(error as Error).message}</p> : null}

      <Card>
        <CardBody className="overflow-x-auto">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead>
              <tr className="border-b border-slate-700 text-slate-400">
                <th className="py-2 pr-4">{t('invoices.number')}</th>
                <th className="py-2 pr-4">{t('invoices.description')}</th>
                <th className="py-2 pr-4">{t('invoices.amount')}</th>
                <th className="py-2 pr-4">{t('invoices.provider')}</th>
                <th className="py-2">{t('invoices.issued')}</th>
              </tr>
            </thead>
            <tbody>
              {invoices.length === 0 && !isLoading ? (
                <tr>
                  <td colSpan={5} className="py-4 text-slate-400">
                    {t('invoices.empty')}
                  </td>
                </tr>
              ) : null}
              {invoices.map((inv) => (
                <tr key={inv.id} className="border-b border-slate-800">
                  <td className="py-2 pr-4 font-mono text-xs text-white">{inv.invoiceNumber}</td>
                  <td className="py-2 pr-4 text-slate-300">{inv.description}</td>
                  <td className="py-2 pr-4 text-white">{formatAmount(inv)}</td>
                  <td className="py-2 pr-4 text-slate-400">{inv.provider}</td>
                  <td className="py-2 text-slate-400">{new Date(inv.issuedAtUtc).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </PageContent>
  )
}
