import type { TrackingData } from '../types'
import { ApiError } from '../../../services/apiClient'

// Backend chỉ có GPS cho Admin (`GET /tracking/boats`); chưa có API theo dõi chuyến cho Passenger/Guest.
export function getTrackingData(): Promise<TrackingData> {
  return Promise.reject(new ApiError(501, 'Backend chưa có API theo dõi chuyến cho Passenger/Guest. Admin xem GPS thật tại Live Operations.'))
}
