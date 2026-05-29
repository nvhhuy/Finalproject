import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import {
  createCreditPackage,
  deleteCreditPackage,
  listCreditPackages,
  updateCreditPackage,
} from '@/shared/api/admin-api'
import { CreditPackageFormDialog } from '@/features/credit-packages/CreditPackageFormDialog'
import { queryKeys } from '@/shared/constants/query-keys'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Badge } from '@/shared/components/ui/Badge'
import { Button } from '@/shared/components/ui/Button'
import { confirmAction, toast, toastApiError } from '@/shared/lib'
import type { CreditPackageDto } from '@/shared/types/api'

function formatPackagePrice(cents: number, currency: string) {
  if (currency === 'VND') {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(
      cents,
    )
  }
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(cents / 100)
  } catch {
    return `${(cents / 100).toFixed(2)} ${currency}`
  }
}

export function CreditPackagesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogMode, setDialogMode] = useState<'create' | 'edit'>('create')
  const [editing, setEditing] = useState<CreditPackageDto | undefined>()

  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.creditPackages.list(),
    queryFn: () => listCreditPackages(),
  })

  const packages = (data ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder)

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.creditPackages.all() })
  }

  const createMutation = useMutation({
    mutationFn: createCreditPackage,
    onSuccess: () => {
      invalidate()
      toast.success(t('creditPackages.created'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const updateMutation = useMutation({
    mutationFn: ({ packageId, body }: { packageId: string; body: Parameters<typeof updateCreditPackage>[1] }) =>
      updateCreditPackage(packageId, body),
    onSuccess: () => {
      invalidate()
      toast.success(t('creditPackages.updated'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const deleteMutation = useMutation({
    mutationFn: deleteCreditPackage,
    onSuccess: () => {
      invalidate()
      toast.success(t('creditPackages.deleted'))
    },
    onError: (err) => toastApiError(err, 'errors.saveFailed'),
  })

  const openCreate = () => {
    setDialogMode('create')
    setEditing(undefined)
    setDialogOpen(true)
  }

  const openEdit = (pkg: CreditPackageDto) => {
    setDialogMode('edit')
    setEditing(pkg)
    setDialogOpen(true)
  }

  const handleDelete = (pkg: CreditPackageDto) => {
    if (!confirmAction(t('creditPackages.deleteConfirm', { name: pkg.name }))) return
    void deleteMutation.mutateAsync(pkg.id)
  }

  return (
    <PageContent>
      <PageHeader
        title={t('creditPackages.title')}
        description={t('creditPackages.subtitle')}
        actions={
          <Button onClick={openCreate}>
            <Plus className="h-4 w-4" />
            {t('creditPackages.add')}
          </Button>
        }
      />

      {error ? <p className="text-sm text-red-600">{t('errors.loadFailed')}</p> : null}
      {isLoading ? <p className="text-sm text-slate-500">{t('common.loading')}</p> : null}

      <Card>
        <CardBody className="divide-y divide-slate-200 p-0">
          {packages.map((pkg) => (
            <div key={pkg.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
              <div>
                <div className="flex items-center gap-2">
                  <p className="font-medium">{pkg.name}</p>
                  <Badge tone={pkg.isActive ? 'success' : 'default'}>
                    {pkg.isActive ? t('creditPackages.active') : t('creditPackages.inactive')}
                  </Badge>
                </div>
                <p className="text-sm text-slate-500">
                  {pkg.code} · {formatPackagePrice(pkg.priceCents, pkg.currency)} → {pkg.creditsAmount}{' '}
                  {t('creditPackages.credits')}
                </p>
                {pkg.description ? <p className="text-sm text-slate-400">{pkg.description}</p> : null}
              </div>
              <div className="flex gap-2">
                <Button size="sm" variant="secondary" onClick={() => openEdit(pkg)}>
                  <Pencil className="h-4 w-4" />
                </Button>
                <Button size="sm" variant="secondary" onClick={() => handleDelete(pkg)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </CardBody>
      </Card>

      <CreditPackageFormDialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        mode={dialogMode}
        pkg={editing}
        onSubmit={async (values) => {
          if (dialogMode === 'create') {
            await createMutation.mutateAsync({
              code: values.code,
              name: values.name,
              description: values.description || null,
              priceCents: values.priceCents,
              currency: values.currency,
              creditsAmount: values.creditsAmount,
              sortOrder: values.sortOrder,
            })
          } else if (editing) {
            await updateMutation.mutateAsync({
              packageId: editing.id,
              body: {
                name: values.name,
                description: values.description || null,
                priceCents: values.priceCents,
                currency: values.currency,
                creditsAmount: values.creditsAmount,
                isActive: values.isActive,
                sortOrder: values.sortOrder,
              },
            })
          }
        }}
      />
    </PageContent>
  )
}
