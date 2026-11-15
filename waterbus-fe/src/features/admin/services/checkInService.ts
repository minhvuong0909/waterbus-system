import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { TripDetailDto, TripStopDto } from '../../trips/types'

/** Các bến của chuyến theo thứ tự ghé, kèm cửa sổ check-in (`GET /trips/{id}`). */
export async function getTripStops(tripId: string): Promise<TripStopDto[]> {
  const trip = await apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId))
  return [...trip.stops].sort((a, b) => a.visitOrder - b.visitOrder)
}

/** Phản hồi của `PUT /checkins/windows/{id}` (BE `SetCheckInWindowResultDto`). */
export interface CheckInWindow {
  tripStopCallId: string
  checkInOpenAt: string
  checkInCloseAt: string
}

/**
 * Đặt khi nào nhân viên được check-in khách tại một bến (BR-BOARD-05: cửa sổ thuộc về bến khách lên). Backend từ
 * chối bến đã rời hoặc bị bỏ qua.
 */
export function setCheckInWindow(tripStopCallId: string, openAt: string, closeAt: string): Promise<CheckInWindow> {
  return apiRequest<CheckInWindow>(API_ENDPOINTS.checkIns.window(tripStopCallId), {
    method: 'PUT',
    body: { tripStopCallId, checkInOpenAt: openAt, checkInCloseAt: closeAt },
  })
}
