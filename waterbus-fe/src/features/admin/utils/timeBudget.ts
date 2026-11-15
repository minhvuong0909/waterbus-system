import type { StopAnnouncementBudget } from '../services/simulatorService'

export interface TimeBudgetInput {
  tripDurationSeconds: number
  dwellTimeSeconds: number
  nearThreshold: number
  segmentCount: number
}

export const DEPARTING_ANNOUNCEMENT_SECONDS = 5
export const NEAR_ANNOUNCEMENT_SECONDS = 4
export const ARRIVED_ANNOUNCEMENT_SECONDS = 4
export const SAFETY_GAP_SECONDS = 1
export const MAX_NARRATION_SECONDS = 10
export const NARRATION_CHARACTERS_PER_SECOND = 14

/**
 * Dựng ngân sách thời gian giống hệt StopAnnouncementTimeBudgetCalculator ở BE.
 * Dùng để cảnh báo tức thì khi Admin đang gõ, không phải chờ khi bấm lưu.
 */
export function previewTimeBudget(input: TimeBudgetInput): StopAnnouncementBudget {
  const segments = Math.max(1, input.segmentCount)
  const dwell = Math.max(0, input.dwellTimeSeconds)
  const threshold = Math.min(0.99, Math.max(0.1, input.nearThreshold))

  const minDwell = ARRIVED_ANNOUNCEMENT_SECONDS + SAFETY_GAP_SECONDS
  const minMoving = Math.max(
    (DEPARTING_ANNOUNCEMENT_SECONDS + MAX_NARRATION_SECONDS + SAFETY_GAP_SECONDS) / threshold,
    (NEAR_ANNOUNCEMENT_SECONDS + SAFETY_GAP_SECONDS) / (1 - threshold),
  )

  const segmentMoving = Math.max(0, (input.tripDurationSeconds - segments * dwell) / segments)
  const departingWindow = segmentMoving * threshold
  const nearWindow = segmentMoving * (1 - threshold)
  const maxNarration = Math.max(
    0,
    Math.min(
      MAX_NARRATION_SECONDS,
      departingWindow - DEPARTING_ANNOUNCEMENT_SECONDS - SAFETY_GAP_SECONDS,
    ),
  )
  const minTripDuration = segments * (minMoving + dwell)

  const violations: { property: string; message: string }[] = []

  if (dwell + 0.001 < minDwell) {
    violations.push({
      property: 'dwellTimeSeconds',
      message: `Thời gian dừng tại bến phải tối thiểu ${minDwell} giây để phát trọn thông báo cập bến.`,
    })
  }
  if (segmentMoving + 0.001 < minMoving) {
    violations.push({
      property: 'tripDurationSeconds',
      message: `Thời lượng toàn chuyến phải tối thiểu ${Math.ceil(minTripDuration)} giây cho ${segments} chặng để thông báo rời bến, thuyết minh địa danh và thông báo gần bến không chồng lên nhau.`,
    })
  }
  if (maxNarration <= 0) {
    violations.push({
      property: 'tripDurationSeconds',
      message: 'Chặng di chuyển quá ngắn nên không thể phát thuyết minh địa danh. Hãy tăng thời lượng toàn chuyến.',
    })
  }

  return {
    segmentCount: segments,
    segmentMovingSeconds: segmentMoving,
    departingWindowSeconds: departingWindow,
    nearWindowSeconds: nearWindow,
    dwellSeconds: dwell,
    maxNarrationSeconds: maxNarration,
    maxNarrationCharacters: Math.floor(maxNarration * NARRATION_CHARACTERS_PER_SECOND),
    minDwellSeconds: minDwell,
    minTripDurationSeconds: minTripDuration,
    isValid: violations.length === 0,
    violations,
  }
}
