import { HOME_DATA } from '../../../mocks/home'
import { CORRIDOR_MAP, NEXT_DEPARTURES } from '../../../mocks/trips'
import { withDelay } from '../../../mocks/delay'
import type { CorridorMap, Departure } from '../../trips/types'
import type { HomeData } from '../types'

// Mock service layer: swap the bodies for real API calls later without touching the UI.
export function getHomeData(): Promise<HomeData> {
  return withDelay(HOME_DATA)
}

export function getNextDepartures(): Promise<Departure[]> {
  return withDelay(NEXT_DEPARTURES)
}

export function getCorridorMap(): Promise<CorridorMap> {
  return withDelay(CORRIDOR_MAP)
}
