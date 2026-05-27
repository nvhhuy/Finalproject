import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Plus, Search, X } from 'lucide-react'
import {
  createUser,
  getUserById,
  listUsers,
  updateUser,
} from '@/shared/api/admin-api'
import { queryKeys } from '@/shared/constants/query-keys'
import { formSelectClass } from '@/shared/constants/styles'
import { PageContent } from '@/shared/components/layout/PageContent'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { DataTablePagination } from '@/shared/components/patterns/DataTablePagination'
import { formatDateOrDash, toast } from '@/shared/lib'
import { Card, CardBody } from '@/shared/components/ui/Card'
import { Button } from '@/shared/components/ui/Button'
import { Badge } from '@/shared/components/ui/Badge'
import { UserFormDialog } from '@/features/users/UserFormDialog'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { usePagination, useTotalPages } from '@/shared/hooks/usePagination'
import { USER_ROLES } from '@/shared/types/api'
import { ApiClientError } from '@/shared/lib/errors'
import i18n from '@/i18n/config'

type LockedFilter = 'all' | 'locked' | 'unlocked'
type SortField = 'username' | 'email' | 'registeredat' | 'role'

const PAGE_SIZE = 20

export function UsersPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { page, setPage, resetPage } = usePagination()
  const [searchInput, setSearchInput] = useState('')
  const [lockedFilter, setLockedFilter] = useState<LockedFilter>('all')
  const [roleFilter, setRoleFilter] = useState<string>('')
  const [sortBy, setSortBy] = useState<SortField>('username')
  const [sortDescending, setSortDescending] = useState(false)

  const [dialogOpen, setDialogOpen] = useState(false)
  const [editingUserId, setEditingUserId] = useState<string | null>(null)

  const debouncedSearch = useDebouncedValue(searchInput.trim(), 350)

  const listParams = {
    page,
    pageSize: PAGE_SIZE,
    search: debouncedSearch || undefined,
    isLocked:
      lockedFilter === 'all' ? undefined : lockedFilter === 'locked',
    role: roleFilter || undefined,
    sortBy,
    sortDescending,
  }

  const { data, isLoading, isFetching } = useQuery({
    queryKey: queryKeys.users.list(listParams),
    queryFn: () => listUsers(listParams),
  })

  const { data: editDetail, isLoading: editLoading } = useQuery({
    queryKey: queryKeys.users.detail(editingUserId),
    queryFn: () => getUserById(editingUserId!),
    enabled: dialogOpen && editingUserId !== null,
  })

  const createMutation = useMutation({
    mutationFn: createUser,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all() })
      toast.success(t('toast.userCreated'))
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({
      id,
      body,
    }: {
      id: string
      body: Parameters<typeof updateUser>[1]
    }) => updateUser(id, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all() })
      toast.success(t('toast.userUpdated'))
    },
  })

  const totalPages = useTotalPages(data?.totalCount, data?.pageSize ?? PAGE_SIZE)
  const hasActiveFilters =
    debouncedSearch.length > 0 || lockedFilter !== 'all' || roleFilter !== ''

  const clearFilters = () => {
    setSearchInput('')
    setLockedFilter('all')
    setRoleFilter('')
    setSortBy('username')
    setSortDescending(false)
    resetPage()
  }

  const resultsSummary =
    data != null
      ? debouncedSearch
        ? t('users.usersFoundFor', { count: data.totalCount, query: debouncedSearch })
        : t('users.usersFound', { count: data.totalCount })
      : null

  return (
    <>
      <PageHeader
        title={t('users.title')}
        description={t('users.description')}
        actions={
          <Button
            onClick={() => {
              setEditingUserId(null)
              setDialogOpen(true)
            }}
          >
            <Plus className="size-4" />
            {t('users.createUser')}
          </Button>
        }
      />
      <PageContent>
        <Card>
          <CardBody className="space-y-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-500" />
              <input
                placeholder={t('users.searchPlaceholder')}
                value={searchInput}
                onChange={(e) => {
                  setSearchInput(e.target.value)
                  resetPage()
                }}
                className="w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] py-2.5 pl-10 pr-10 text-sm text-slate-100 placeholder:text-slate-500 focus:border-[var(--color-accent)] focus:outline-none focus:ring-1 focus:ring-[var(--color-accent)]"
              />
              {searchInput ? (
                <button
                  type="button"
                  onClick={() => {
                    setSearchInput('')
                    resetPage()
                  }}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-300"
                  aria-label={t('common.clearSearch')}
                >
                  <X className="size-4" />
                </button>
              ) : null}
            </div>

            <div className="flex flex-wrap items-end gap-3">
              <div className="space-y-1">
                <label htmlFor="locked-filter" className="block text-xs text-slate-500">
                  {t('users.status')}
                </label>
                <select
                  id="locked-filter"
                  value={lockedFilter}
                  onChange={(e) => {
                    setLockedFilter(e.target.value as LockedFilter)
                    resetPage()
                  }}
                  className={formSelectClass}
                >
                  <option value="all">{t('users.statusAll')}</option>
                  <option value="locked">{t('users.statusLocked')}</option>
                  <option value="unlocked">{t('users.statusActive')}</option>
                </select>
              </div>

              <div className="space-y-1">
                <label htmlFor="role-filter" className="block text-xs text-slate-500">
                  {t('users.role')}
                </label>
                <select
                  id="role-filter"
                  value={roleFilter}
                  onChange={(e) => {
                    setRoleFilter(e.target.value)
                    resetPage()
                  }}
                  className={formSelectClass}
                >
                  <option value="">{t('users.allRoles')}</option>
                  {USER_ROLES.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
              </div>

              <div className="space-y-1">
                <label htmlFor="sort-by" className="block text-xs text-slate-500">
                  {t('users.sortBy')}
                </label>
                <select
                  id="sort-by"
                  value={sortBy}
                  onChange={(e) => {
                    setSortBy(e.target.value as SortField)
                    resetPage()
                  }}
                  className={formSelectClass}
                >
                  <option value="username">{t('users.sortUsername')}</option>
                  <option value="email">{t('users.sortEmail')}</option>
                  <option value="registeredat">{t('users.sortRegistered')}</option>
                  <option value="role">{t('users.role')}</option>
                </select>
              </div>

              <div className="space-y-1">
                <label htmlFor="sort-dir" className="block text-xs text-slate-500">
                  {t('users.order')}
                </label>
                <select
                  id="sort-dir"
                  value={sortDescending ? 'desc' : 'asc'}
                  onChange={(e) => {
                    setSortDescending(e.target.value === 'desc')
                    resetPage()
                  }}
                  className={formSelectClass}
                >
                  <option value="asc">{t('users.ascending')}</option>
                  <option value="desc">{t('users.descending')}</option>
                </select>
              </div>

              {hasActiveFilters ? (
                <Button variant="ghost" size="sm" onClick={clearFilters}>
                  {t('users.clearFilters')}
                </Button>
              ) : null}
            </div>

            <p className="text-xs text-slate-500">
              {isFetching && !isLoading ? t('common.updating') : null}
              {resultsSummary}
            </p>
          </CardBody>
        </Card>

        <Card>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--color-border)] text-slate-400">
                <tr>
                  <th className="px-5 py-3 font-medium">{t('users.colUser')}</th>
                  <th className="px-5 py-3 font-medium">{t('users.colRole')}</th>
                  <th className="px-5 py-3 font-medium">{t('users.colStatus')}</th>
                  <th className="hidden px-5 py-3 font-medium md:table-cell">
                    {t('users.colRegistered')}
                  </th>
                  <th className="px-5 py-3 font-medium" />
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-8 text-center text-slate-400">
                      {t('common.loading')}
                    </td>
                  </tr>
                ) : data?.items.length ? (
                  data.items.map((user) => (
                    <tr
                      key={user.id}
                      className="border-b border-[var(--color-border)]/60 hover:bg-[var(--color-surface-muted)]/40"
                    >
                      <td className="px-5 py-3">
                        <p className="font-medium text-slate-100">{user.userName}</p>
                        <p className="text-xs text-slate-500">{user.email}</p>
                      </td>
                      <td className="px-5 py-3">
                        <Badge tone={user.role === 'Admin' ? 'warning' : 'default'}>
                          {user.role}
                        </Badge>
                      </td>
                      <td className="px-5 py-3">
                        {user.isLocked ? (
                          <Badge tone="danger">{t('users.locked')}</Badge>
                        ) : user.emailVerifiedAtUtc ? (
                          <Badge tone="success">{t('users.verified')}</Badge>
                        ) : (
                          <Badge tone="warning">{t('users.unverified')}</Badge>
                        )}
                      </td>
                      <td className="hidden px-5 py-3 text-slate-400 md:table-cell">
                        {formatDateOrDash(user.registeredAtUtc)}
                      </td>
                      <td className="px-5 py-3 text-right">
                        <div className="flex justify-end gap-2">
                          <Button
                            size="sm"
                            variant="secondary"
                            onClick={() => {
                              setEditingUserId(user.id)
                              setDialogOpen(true)
                            }}
                          >
                            {t('common.edit')}
                          </Button>
                          <Link to={`/users/${user.id}`}>
                            <Button size="sm" variant="ghost">
                              {t('users.manage')}
                            </Button>
                          </Link>
                        </div>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={5} className="px-5 py-8 text-center text-slate-400">
                      {t('users.noResults')}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <DataTablePagination
            page={page}
            totalPages={totalPages}
            onPageChange={setPage}
            className="sm:px-5"
          />
        </Card>
      </PageContent>

      <UserFormDialog
        open={dialogOpen}
        onClose={() => {
          setDialogOpen(false)
          setEditingUserId(null)
        }}
        mode={editingUserId ? 'edit' : 'create'}
        detail={editDetail}
        detailLoading={editLoading && editingUserId !== null}
        onSubmit={async (values) => {
          try {
            if (editingUserId) {
              if (!editDetail) {
                throw new ApiClientError({
                  code: 'MissingRowVersion',
                  message: i18n.t('errors.missingRowVersion'),
                  httpStatus: null,
                })
              }
              await updateMutation.mutateAsync({
                id: editingUserId,
                body: {
                  firstName: values.firstName,
                  lastName: values.lastName,
                  userName: values.userName,
                  email: values.email,
                  role: values.role,
                  rowVersion: editDetail.rowVersion,
                },
              })
            } else {
              await createMutation.mutateAsync({
                firstName: values.firstName,
                lastName: values.lastName,
                userName: values.userName,
                email: values.email,
                password: values.password,
                role: values.role,
              })
            }
          } catch (err) {
            throw err instanceof ApiClientError ? err : new Error(i18n.t('errors.requestFailed'))
          }
        }}
      />
    </>
  )
}
