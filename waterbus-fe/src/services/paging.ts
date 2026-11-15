import { apiRequest } from './apiClient'

type Query = Record<string, string | number | boolean | undefined>

/** Số dòng mỗi lần đọc; backend cho tối đa 100. */
const PAGE_SIZE = 100

/**
 * Đọc hết các trang của một danh sách phân trang (BE `PaginatedList<T>`, tham số `pageNumber`/`pageSize`) rồi trả
 * về một mảng phẳng. Chỉ dùng cho danh sách ngắn (danh mục, tàu, sự cố đang mở); danh sách dài phải phân trang ở UI.
 */
export async function fetchAllPages<T>(path: string, query: Query = {}): Promise<T[]> {
  const rows: T[] = []
  for (let pageNumber = 1; ; pageNumber++) {
    const page = await apiRequest<{ items: T[]; totalCount: number }>(path, {
      query: { ...query, pageNumber, pageSize: PAGE_SIZE },
    })
    rows.push(...page.items)
    if (!page.items.length || rows.length >= page.totalCount) return rows
  }
}
