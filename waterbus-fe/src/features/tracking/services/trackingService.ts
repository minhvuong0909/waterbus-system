import { withDelay } from '../../../mocks/delay'
import { TRACKING_DATA } from '../../../mocks/tracking'
import type { TrackingData } from '../types'
import { USE_MOCK } from '../../../constant/env'
import { ApiError } from '../../../services/apiClient'

// Mock service layer: swap for a real live-position feed later without touching the UI.
export function getTrackingData(): Promise<TrackingData> {
  if (!USE_MOCK) return Promise.reject(new ApiError(501, 'Backend chưa có API theo dõi chuyến cho Passenger/Guest. Admin xem GPS thật tại Live Operations.'))
  return withDelay(TRACKING_DATA)
}
