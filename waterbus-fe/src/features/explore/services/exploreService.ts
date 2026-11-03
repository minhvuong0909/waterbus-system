import { EXPLORE_DATA } from '../../../content/explore'
import { t } from '../../../i18n'
import { toVietnamDate } from '../../../utils/time'
import { getPierOptions, getTrips, isTripOnSale } from '../../trips/services/tripService'
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
 * The next sightseeing trips that can still be booked, in both directions of the corridor, from
 * `GET /trips/search`. Sightseeing runs its whole route (BR-ROUTE-05), so only the first and last
 * pier are searched. Days are read one after another and the search stops once `limit` is reached.
 * A trip whose sales the backend closed is left out, since every card invites to book.
 */
export async function getSightseeingDepartures(
  limit = 4,
  now = new Date(),
): Promise<SightseeingDeparture[]> {
  const piers = await getPierOptions()
  if (piers.length < 2) return []
  const ends = [
    [piers[0], piers[piers.length - 1]],
    [piers[piers.length - 1], piers[0]],
  ] as const
  const today = toVietnamDate(now)
  const found: SightseeingDeparture[] = []

  for (let day = 0; day < SEARCH_DAYS && found.length < limit; day++) {
    const dateId = toVietnamDate(new Date(now.getTime() + day * DAY_MS))
    const lists = await Promise.all(
      ends.map(async ([origin, destination]) => {
        const trips = await getTrips({
          tripType: 'one-way',
          originId: origin.id,
          destinationId: destination.id,
          dateId,
        })
        return trips
          .filter(
            (trip) =>
              trip.kind === 'sightseeing' && trip.seatsCount > 0 && !isDeparted(trip, dateId, now),
          )
          .map((trip) => ({
            departure: toDeparture(trip, origin, destination, now, dateId === today),
            search: { originId: origin.id, destinationId: destination.id, dateId },
          }))
      }),
    )
    const candidates = lists.flat().sort((a, b) => a.departure.time.localeCompare(b.departure.time))
    // The search does not say whether sales are open: keep only trips the backend still sells.
    const onSale = await Promise.all(candidates.map((c) => isTripOnSale(c.departure.trip.id)))
    found.push(...candidates.filter((_, i) => onSale[i]))
  }
  return found.slice(0, limit)
}
