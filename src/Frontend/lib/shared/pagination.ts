/** 1-based page index. */
export function calcTotalPages(totalCount: number, pageSize: number): number {
  if (pageSize <= 0) return 0;
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

export function clampPage(page: number, totalPages: number): number {
  if (totalPages <= 0) return 1;
  return Math.min(Math.max(1, page), totalPages);
}
