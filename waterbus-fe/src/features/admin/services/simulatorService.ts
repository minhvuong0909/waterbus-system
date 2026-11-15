import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'

/** Vi phạm ràng buộc ngân sách thời gian, gắn với trường cấu hình tương ứng. */
export interface StopAnnouncementBudgetViolation {
  property: string
  message: string
}

/**
 * Ngân sách thời gian của một chặng, do BE dựng từ cấu hình hiện tại.
 * Đây là nguồn sự thật duy nhất để giới hạn độ dài kịch bản thuyết minh.
 */
export interface StopAnnouncementBudget {
  segmentCount: number
  segmentMovingSeconds: number
  departingWindowSeconds: number
  nearWindowSeconds: number
  dwellSeconds: number
  maxNarrationSeconds: number
  maxNarrationCharacters: number
  minDwellSeconds: number
  minTripDurationSeconds: number
  isValid: boolean
  violations: StopAnnouncementBudgetViolation[]
}

export interface StopAnnouncementConfig {
  tripDurationSeconds: number
  dwellTimeSeconds: number
  nearThreshold: number
  useSimulatedSegmentDuration: boolean
  budget: StopAnnouncementBudget
}

/** Admin: lấy cấu hình thông báo bến và mô phỏng hành trình hiện tại. */
export async function getSimulatorConfig(): Promise<StopAnnouncementConfig> {
  return apiRequest<StopAnnouncementConfig>(API_ENDPOINTS.admin.simulatorStopAnnouncement)
}

/** Admin: cập nhật cấu hình thông báo bến và tốc độ mô phỏng hành trình. */
export async function updateSimulatorConfig(payload: StopAnnouncementConfig): Promise<StopAnnouncementConfig> {
  return apiRequest<StopAnnouncementConfig>(API_ENDPOINTS.admin.simulatorStopAnnouncement, {
    method: 'PUT',
    body: {
      tripDurationSeconds: payload.tripDurationSeconds,
      dwellTimeSeconds: payload.dwellTimeSeconds,
      nearThreshold: payload.nearThreshold,
      useSimulatedSegmentDuration: payload.useSimulatedSegmentDuration,
    },
  })
}
