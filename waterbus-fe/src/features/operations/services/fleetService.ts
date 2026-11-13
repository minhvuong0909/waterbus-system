import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat } from '../types'

const PAGE_SIZE = 100

/** `GET /boats`: every active boat, reading all pages. */
export async function getBoats(): Promise<AdminBoat[]> {
  const boats: AdminBoat[] = []
  for (let pageNumber = 1; ; pageNumber++) {
    const page = await apiRequest<{ items: AdminBoat[]; totalCount: number }>(API_ENDPOINTS.boats, {
      query: { pageNumber, pageSize: PAGE_SIZE, isActiveOnly: true },
    })
    boats.push(...page.items)
    if (!page.items.length || boats.length >= page.totalCount) return boats
  }
}
