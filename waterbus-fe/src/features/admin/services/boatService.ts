import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat, NewBoat } from '../types'

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

/** `POST /boats` without a captain, then reads the saved boat back. */
export async function createBoat(input: NewBoat): Promise<AdminBoat> {
  const id = await apiRequest<string>(API_ENDPOINTS.boats, { method: 'POST', body: { ...input, captainUserId: null } })
  return apiRequest<AdminBoat>(API_ENDPOINTS.boat(id))
}

/** Soft delete: the backend has no DELETE, so the boat is deactivated with `PUT`. */
export async function deleteBoat(id: string): Promise<void> {
  const boat = await apiRequest<AdminBoat>(API_ENDPOINTS.boat(id))
  await apiRequest(API_ENDPOINTS.boat(id), { method: 'PUT', body: { ...boat, isActive: false } })
}
