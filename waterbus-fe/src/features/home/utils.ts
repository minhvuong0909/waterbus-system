import { t } from '../../i18n'
import type { Departure, PierOption, Trip } from '../trips/types'
import { seatsLabel } from '../trips/utils'

/** A departure leaves within this many minutes -> shown as "Boarding". */
const BOARDING_WINDOW_MINUTES = 15

/** Home page row for a trip. `origin` / `destination` are the piers of the search it came from. */
export function toDeparture(
  trip: Trip,
  origin: PierOption,
  destination: PierOption,
  now = new Date(),
  /** False for trips of another day: they cannot be "Boarding" just because the clock time is near. */
  isToday = true,
): Departure {
  const [hours, minutes] = trip.departTime.split(':').map(Number)
  const minutesToGo = hours * 60 + minutes - (now.getHours() * 60 + now.getMinutes())
  const sightseeing = trip.kind === 'sightseeing'
  const boarding = isToday && minutesToGo <= BOARDING_WINDOW_MINUTES
  return {
    id: `dep-${trip.id}`,
    trip,
    time: trip.departTime,
    status: boarding ? t('Boarding') : sightseeing && trip.band === 'sunset' ? t('Twilight') : t('Scheduled'),
    statusKind: boarding ? 'boarding' : sightseeing && trip.band === 'sunset' ? 'twilight' : 'scheduled',
    kind: trip.kind,
    from: origin.shortName,
    to: destination.shortName,
    details: [t('{durationMins} min voyage', { durationMins: trip.durationMins }), seatsLabel(trip)],
    priceVnd: trip.fareVnd,
  }
}

