import { withDelay } from '../../../mocks/delay'
import { TRACKING_DATA } from '../../../mocks/tracking'
import type { TrackingData } from '../types'

// Mock service layer: swap for a real live-position feed later without touching the UI.
export function getTrackingData(): Promise<TrackingData> {
  return withDelay(TRACKING_DATA)
}
