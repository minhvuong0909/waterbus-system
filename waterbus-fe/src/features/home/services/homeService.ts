import { HOME_DATA } from '../../../content/home'
import { localize } from '../../../i18n'
import { getLowestFares } from '../../trips/services/fareService'
import { getPierOptions, getUpcomingTrips } from '../../trips/services/tripService'
import type { CorridorMap, Departure, SearchQuery } from '../../trips/types'
import type { HomeData } from '../types'
import { buildCorridorMap, toDeparture } from '../utils'

const MAX_DEPARTURES = 4

// Editorial content (hero, stories, steps) has no backend source and stays static. Prices do not:
// with the real backend a tour shows the lowest Sightseeing fare of the database, and no duration.
export async function getHomeData(): Promise<HomeData> {
  const fares = await getLowestFares().catch(() => ({}) as Awaited<ReturnType<typeof getLowestFares>>)
  const stories = HOME_DATA.stories.map((story) => ({
    ...story,
    fromPriceVnd: fares.Sightseeing,
    durationMins: undefined,
  }))
  return localize({ ...HOME_DATA, stories })
}

export interface NextDepartures {
  items: Departure[]
  /** True when today has no departure left and these are tomorrow's. */
  tomorrow: boolean
  /** The piers and day these departures came from, so the timetable page can open the same search. */
  search?: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>
}

/** The next departures of today (or tomorrow's when today is over), from the trip search. */
export async function getNextDepartures(
  pair?: Pick<SearchQuery, 'originId' | 'destinationId'>,
): Promise<NextDepartures> {
  const { trips, piers, search, tomorrow } = await getUpcomingTrips(MAX_DEPARTURES, pair)
  const origin = piers.find((p) => p.id === search.originId)
  const destination = piers.find((p) => p.id === search.destinationId)
  if (!origin || !destination) return { items: [], tomorrow: false }
  return {
    items: trips.map((trip) => toDeparture(trip, origin, destination, new Date(), !tomorrow)),
    tomorrow,
    search,
  }
}

/** The stations of the corridor, read from `GET /stations`. */
export async function getCorridorMap(): Promise<CorridorMap> {
  return buildCorridorMap(await getPierOptions())
}
