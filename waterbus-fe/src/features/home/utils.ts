import { t } from '../../i18n'
import { positionAlong } from '../admin/utils'
import type { CorridorMap, Departure, PierOption, Trip } from '../trips/types'
import { seatsLabel } from '../trips/utils'

/** A departure leaves within this many minutes -> shown as "Boarding". */
const BOARDING_WINDOW_MINUTES = 15

/** Home page row for a trip. `origin` / `destination` are the piers of the search it came from. */
export function toDeparture(trip: Trip, origin: PierOption, destination: PierOption, now = new Date()): Departure {
  const [hours, minutes] = trip.departTime.split(':').map(Number)
  const minutesToGo = hours * 60 + minutes - (now.getHours() * 60 + now.getMinutes())
  const sightseeing = trip.kind === 'sightseeing'
  const boarding = minutesToGo <= BOARDING_WINDOW_MINUTES
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

/** Where the demo vessel is on the corridor (0-1). Mock until the tracking API exists. */
const MOCK_VESSEL_PROGRESS = 0.45
/** Route RT01 is 10.8 km and takes 45 minutes. */
const MOCK_VESSEL_SPEED_KMH = 14.4

/** The river on the map artwork, as points in percent of the canvas from the first pier to the last. */
const RIVER_PATH: [number, number][] = [
  [9, 10],
  [32, 35],
  [62, 57],
  [74, 70],
  [78, 84],
]

/**
 * The corridor drawn on the home page map. The map is a schematic, so the piers are spread along
 * the river artwork in river order (not by their coordinates), and a mock vessel sails part-way.
 */
export function buildCorridorMap(piers: PierOption[]): CorridorMap {
  const placed = piers.map((pier, index) => {
    const [x, y] = positionAlong(RIVER_PATH, piers.length > 1 ? index / (piers.length - 1) : 0)
    return {
      id: pier.id,
      name: pier.name,
      subtitle: pier.district,
      x,
      y,
      kind: (index === 0 ? 'hub' : index === piers.length - 1 ? 'terminus' : 'stop') as 'hub' | 'stop' | 'terminus',
    }
  })
  const [x, y] = positionAlong(RIVER_PATH, MOCK_VESSEL_PROGRESS)
  const next = piers[Math.min(Math.ceil(MOCK_VESSEL_PROGRESS * (piers.length - 1)), piers.length - 1)]

  return {
    piers: placed,
    vessel: {
      code: 'SWB-01',
      status: t('On time'),
      speedKmh: MOCK_VESSEL_SPEED_KMH,
      nextPier: next?.shortName ?? '',
      x,
      y,
    },
  }
}
