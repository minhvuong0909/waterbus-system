import { HOME_DATA } from '../../../mocks/home'
import { withDelay } from '../../../mocks/delay'
import { getPierOptions, getUpcomingTrips } from '../../trips/services/tripService'
import type { CorridorMap, Departure } from '../../trips/types'
import type { HomeData } from '../types'
import { buildCorridorMap, toDeparture } from '../utils'

const MAX_DEPARTURES = 4

// Editorial content (hero, stories, steps): no backend source, so it stays a mock.
export function getHomeData(): Promise<HomeData> {
  return withDelay(HOME_DATA)
}

/** The next departures of today, from the trip search. */
export async function getNextDepartures(): Promise<Departure[]> {
  const { trips, piers, search } = await getUpcomingTrips(MAX_DEPARTURES)
  const origin = piers.find((p) => p.id === search.originId)
  const destination = piers.find((p) => p.id === search.destinationId)
  if (!origin || !destination) return []
  return trips.map((trip) => toDeparture(trip, origin, destination))
}

/** The stations of the corridor. The vessel on it is a mock until the tracking API exists. */
export async function getCorridorMap(): Promise<CorridorMap> {
  return buildCorridorMap(await getPierOptions())
}
