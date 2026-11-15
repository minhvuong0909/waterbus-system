import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { fetchAllPages } from '../../../services/paging'
import { clearPierCache } from '../../trips/services/tripService'
import type { AdminStation, NewStation } from '../types'

/**
 * Mọi bến Admin quản lý, đang hoạt động lẫn đã ngừng, theo thứ tự dọc sông. `GET /stations` (dùng ở nơi khác, vd
 * tìm chuyến của hành khách) chỉ trả bến đang hoạt động, dùng ở đây thì bến đã ngừng biến mất khỏi trang và không
 * bật lại được. Vì vậy đọc `/stations/paged` (danh mục nhỏ nên đọc hết các trang rồi trả mảng phẳng).
 */
export async function getStations(): Promise<AdminStation[]> {
  const stations = await fetchAllPages<AdminStation>(API_ENDPOINTS.stationsPaged)
  return stations.sort((a, b) => a.orderIndex - b.orderIndex)
}

/** `POST /stations`, rồi đọc lại bến vừa lưu. */
export async function createStation(input: NewStation): Promise<AdminStation> {
  const id = await apiRequest<string>(API_ENDPOINTS.stations, { method: 'POST', body: input })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}

/** `PUT /stations/{id}`, giữ nguyên trạng thái hoạt động của bến. */
export async function updateStation(id: string, input: NewStation): Promise<AdminStation> {
  const current = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...input, id, isActive: current.isActive } })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}
