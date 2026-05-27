import { useCallback, useState } from 'react'
import { calcTotalPages } from '@/shared/lib/pagination'

export function usePagination(initialPage = 1) {
  const [page, setPage] = useState(initialPage)

  const resetPage = useCallback(() => setPage(1), [])

  return { page, setPage, resetPage }
}

export function useTotalPages(totalCount: number | undefined, pageSize: number) {
  return totalCount != null ? calcTotalPages(totalCount, pageSize) : 0
}
