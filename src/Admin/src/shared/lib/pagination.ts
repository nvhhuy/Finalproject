export function calcTotalPages(totalCount: number, pageSize: number): number {
  if (pageSize <= 0) return 0
  return Math.ceil(totalCount / pageSize)
}
