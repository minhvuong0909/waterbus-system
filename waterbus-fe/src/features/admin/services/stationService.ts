import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { clearPierCache } from '../../trips/services/tripService'
import type { AdminStation, NewStation } from '../types'

/**
 * Every station the Admin manages, active and inactive, in river order. `GET /stations` (used
 * elsewhere, e.g. for Passenger search) only returns ACTIVE ones - using it here meant a
 * deactivated station disappeared from this page with no way to see or re-activate it. Reads
 * `/stations/paged` instead (catalogue is tiny, so read every page and return it flat).
 */
export async function getStations(): Promise<AdminStation[]> {
  const stations: AdminStation[] = []
  for (let pageNumber = 1; ; pageNumber++) {
    const page = await apiRequest<{ items: AdminStation[]; totalCount: number }>(API_ENDPOINTS.stationsPaged, {
      query: { pageNumber, pageSize: 100 },
    })
    stations.push(...page.items)
    if (!page.items.length || stations.length >= page.totalCount) return stations.sort((a, b) => a.orderIndex - b.orderIndex)
  }
}

/** `POST /stations`, then reads the saved station back. */
export async function createStation(input: NewStation): Promise<AdminStation> {
  const id = await apiRequest<string>(API_ENDPOINTS.stations, { method: 'POST', body: input })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}

/** `PUT /stations/{id}`, keeping the station's active flag as it is. */
export async function updateStation(id: string, input: NewStation): Promise<AdminStation> {
  const current = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...input, id, isActive: current.isActive } })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}

/** Soft delete: the backend has no DELETE, so the station is deactivated with `PUT`. */
export async function deleteStation(id: string): Promise<void> {
  await setStationActive(id, false)
}

/** Toggles a station active/inactive with `PUT` (the backend has no dedicated activate/deactivate route). */
export async function setStationActive(id: string, active: boolean): Promise<void> {
  const station = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...station, isActive: active } })
  clearPierCache()
}
