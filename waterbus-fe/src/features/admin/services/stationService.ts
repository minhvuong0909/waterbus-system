import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { clearPierCache } from '../../trips/services/tripService'
import type { AdminStation, NewStation } from '../types'

/** `GET /stations`: the stations in river order. */
export const getStations = () => apiRequest<AdminStation[]>(API_ENDPOINTS.stations)

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
  const station = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...station, isActive: false } })
  clearPierCache()
}
