import { EXPLORE_DATA } from '../../../content/explore'
import { t } from '../../../i18n'
import { toVietnamDate } from '../../../utils/time'
import { getPierOptions, getSightseeingTours, isTripOnSale } from '../../trips/services/tripService'
import { isDeparted, toDeparture } from '../../trips/utils'
import type { ExploreData, Landmark, SightseeingDeparture } from '../types'

/** Days ahead searched for sightseeing departures, today included. */
const SEARCH_DAYS = 3
const DAY_MS = 24 * 60 * 60 * 1000

/** A place with its texts in the active language; `category` stays a code (filters, icons). */
export const translateLandmark = (place: Landmark): Landmark => ({
  ...place,
  title: t(place.title),
  description: t(place.description),
})

// The places are editorial (no public POI API yet), so they are returned right away, translated.
export function getExploreData(): Promise<ExploreData> {
  return Promise.resolve({ ...EXPLORE_DATA, landmarks: EXPLORE_DATA.landmarks.map(translateLandmark) })
}

/**
 * The next sightseeing trips that can still be booked, from `GET /trips/search`. A sightseeing trip runs its
 * whole route (BR-ROUTE-05), so it is listed with the two ends of that route, which may be the same pier when
 * the trip loops back (BR-ROUTE-06). Days are read one after another and the search stops once `limit` is reached.
 * A trip whose sales the backend closed is left out, since every card invites to book.
 */
export async function getSightseeingDepartures(
  limit = 4,
  now = new Date(),
): Promise<SightseeingDeparture[]> {
  const piers = await getPierOptions()
  const pierOf = (id: string) => piers.find((pier) => pier.id.toLowerCase() === id.toLowerCase())
  const today = toVietnamDate(now)
  const found: SightseeingDeparture[] = []

  for (let day = 0; day < SEARCH_DAYS && found.length < limit; day++) {
    const dateId = toVietnamDate(new Date(now.getTime() + day * DAY_MS))
    const tours = await getSightseeingTours(dateId)
    const candidates = tours
      .filter((trip) => trip.tour && trip.seatsCount > 0 && !isDeparted(trip, dateId, now))
      .flatMap((trip) => {
        const { originId, destinationId } = trip.tour!
        const origin = pierOf(originId)
        const destination = pierOf(destinationId)
        return origin && destination
          ? [{ departure: toDeparture(trip, origin, destination, now, dateId === today), search: { originId, destinationId, dateId } }]
          : []
      })
      .sort((a, b) => a.departure.time.localeCompare(b.departure.time))
    // The search does not say whether sales are open: keep only trips the backend still sells.
    const onSale = await Promise.all(candidates.map((c) => isTripOnSale(c.departure.trip.id)))
    found.push(...candidates.filter((_, i) => onSale[i]))
  }
  return found.slice(0, limit)
}
