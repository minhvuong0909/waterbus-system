import { EXPLORE_DATA } from '../../../content/explore'
import { HOME_DATA } from '../../../content/home'
import { localize } from '../../../i18n'
import { getPierOptions, getUpcomingTrips } from '../../trips/services/tripService'
import type { CorridorMap, Departure, SearchQuery } from '../../trips/types'
import { translateLandmark } from '../../explore/services/exploreService'
import type { Landmark } from '../../explore/types'
import type { HomeData } from '../types'
import { buildCorridorMap, toDeparture } from '../../trips/utils'

const MAX_DEPARTURES = 4

/** Editorial content of the home page (timetable filters, how it works); no backend source. */
export function getHomeData(): Promise<HomeData> {
  return Promise.resolve(localize(HOME_DATA))
}

/** Places narrated on sightseeing trips that have a photo, shown as a teaser of the Explore page. */
export function getFeaturedPlaces(count = 3): Landmark[] {
  return EXPLORE_DATA.landmarks
    .filter((place) => place.image)
    .slice(0, count)
    .map(translateLandmark)
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
