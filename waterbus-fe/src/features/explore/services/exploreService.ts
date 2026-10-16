import { withDelay } from '../../../mocks/delay'
import { EXPLORE_DATA } from '../../../mocks/explore'
import type { ExploreData } from '../types'

// Mock service layer: swap for real API calls later without touching the UI.
export function getExploreData(): Promise<ExploreData> {
  return withDelay(EXPLORE_DATA)
}
