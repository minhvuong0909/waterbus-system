import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdHocTripInput, AssignBoatResult, GenerateTripsInput, GenerateTripsResult } from '../types'

// Chuyến mới bắt đầu ở `Scheduled` và chưa có tàu, nên chưa mở bán cho đến khi được gán tàu
// (BR-SCH-04). Việc đổi trạng thái thuộc về operations service.

/**
 * `POST /admin/trips/generate`: tạo các chuyến của một lịch chạy cho một khoảng ngày. Ngày đã có chuyến, ngày đã
 * qua hoặc ngày không chạy được bỏ qua, nên chạy lại vẫn an toàn.
 */
export const generateTrips = (input: GenerateTripsInput) =>
  apiRequest<GenerateTripsResult>(API_ENDPOINTS.admin.generateTrips, { method: 'POST', body: input })

/** `POST /admin/trips` (BR-SCH-10): một chuyến ngoài lịch chạy, trên một tuyến và các bến của nó. */
export const createAdHocTrip = (input: AdHocTripInput) =>
  apiRequest<{ tripId: string; departureTime: string }>(API_ENDPOINTS.admin.trips, {
    method: 'POST',
    body: input,
  })

/**
 * `PUT /admin/trips/{tripId}/boat`: gán tàu cho chuyến `Scheduled` hoặc thay tàu. Backend từ chối (409) tàu đang
 * chạy chuyến trùng giờ (BR-SCH-08) hoặc thiếu ghế cùng mã và hạng cho ghế đã bán (BR-SCH-07); ghế đã bán được
 * chuyển sang tàu mới.
 */
export const assignTripBoat = (tripId: string, boatId: string) =>
  apiRequest<AssignBoatResult>(API_ENDPOINTS.admin.tripBoat(tripId), { method: 'PUT', body: { boatId } })
