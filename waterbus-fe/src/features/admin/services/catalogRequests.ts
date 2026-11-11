import { apiRequest } from '../../../services/apiClient'

const PAGE_SIZE = 100

type Query = Record<string, string | number | boolean | undefined>

/** Reads every page of a paged admin list (BE `PaginatedList<T>`); catalogue lists are short. */
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

/** `POST {item}/activate` or `{item}/deactivate`: catalogue items are switched off, never deleted. */
export function setActive<T = void>(itemPath: string, active: boolean): Promise<T> {
  return apiRequest<T>(`${itemPath}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' })
}
