import { numberLocale, t } from '../../i18n'
import { formatClock, toIsoDate } from '../../utils/time'
import type {
  DateOption,
  PierOption,
  SearchQuery,
  StationDto,
  TimeBand,
  Trip,
  TripDto,
} from './types'

/** At or below this many free seats a trip is shown as "Limited". */
export const LOW_SEATS_THRESHOLD = 10

/** How many days the date strip offers, starting today. */
export const SEARCH_WINDOW_DAYS = 7

/** "Today, Dec 16" for today's date, otherwise "Tue, Dec 17". `date` must already be translated. */
export function formatDateLabel(date: DateOption): string {
  return date.isToday
    ? t('Today, {label}', { label: date.label })
    : t('{weekday}, {label}', { weekday: date.weekday, label: date.label })
}

/** "28 Seats Left", or "54 Seats Available" when plenty remain. */
export function seatsLabel(trip: Pick<Trip, 'seatsCount' | 'seatsLow'>): string {
  if (trip.seatsLow) return t('Limited: Only {count} Seats Left', { count: trip.seatsCount })
  return trip.seatsCount >= 50
    ? t('{count} Seats Available', { count: trip.seatsCount })
    : t('{count} Seats Left', { count: trip.seatsCount })
}

/**
 * Pier shown in forms, derived from a backend station. "Bến Bạch Đằng" becomes the short name
 * "Bạch Đằng", and the district is the last part of the address ("Quận 1").
 */
export function toPierOption(station: StationDto): PierOption {
  const parts = station.address.split(',')
  return {
    id: station.id,
    code: station.code,
    name: station.name,
    pierName: station.name,
    district: parts[parts.length - 1].trim(),
    shortName: station.name.replace(/^Bến\s+/i, ''),
    subtitle: station.address,
  }
}

/**
 * Departure/arrival piers for `query`. Ids that are empty or unknown fall back to the first
 * and last pier of the corridor, so a fresh visit still has a valid pair.
 */
export function pickPiers(
  piers: PierOption[],
  query: Pick<SearchQuery, 'originId' | 'destinationId'>,
): { originId: string; destinationId: string } {
  const known = (id: string) => piers.some((p) => p.id === id)
  const originId = known(query.originId) ? query.originId : piers[0]?.id ?? ''
  const fallbackDestination = piers[piers.length - 1]?.id ?? ''
  const destinationId =
    known(query.destinationId) && query.destinationId !== originId
      ? query.destinationId
      : fallbackDestination !== originId
      ? fallbackDestination
      : piers[0]?.id ?? ''
  return { originId, destinationId }
}

/** Morning until noon, afternoon until 17:00, then the sunset band. */
export function timeBandOf(hour: number): TimeBand {
  if (hour < 12) return 'morning'
  return hour < 17 ? 'afternoon' : 'sunset'
}

/** Short reference for a trip, the first 8 characters of its id (e.g. "3F2A9C1B"). */
export function tripReference(id: string): string {
  return id.replace(/-/g, '').slice(0, 8).toUpperCase()
}

/** Date option for a `YYYY-MM-DD` id, formatted for the active language. */
export function describeDate(id: string, today = toIsoDate(new Date())): DateOption {
  const [year, month, day] = id.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  const locale = numberLocale()
  return {
    id,
    weekday: date.toLocaleDateString(locale, { weekday: 'short' }),
    label: date.toLocaleDateString(locale, { month: 'short', day: 'numeric' }),
    fullLabel: date.toLocaleDateString(locale, { year: 'numeric', month: 'short', day: 'numeric' }),
    isToday: id === today,
  }
}

/** The next `days` days starting today. */
export function buildDateOptions(days = SEARCH_WINDOW_DAYS): DateOption[] {
  const today = new Date()
  return Array.from({ length: days }, (_, offset) => {
    const date = new Date(today.getFullYear(), today.getMonth(), today.getDate() + offset)
    return describeDate(toIsoDate(date))
  })
}

/**
 * Trip card data from a backend TripDto. The backend does not send boat, gate, amenity or stop
 * information yet, so those fields are left empty or neutral.
 */
export function toTrip(dto: TripDto): Trip {
  const depart = new Date(dto.departureTime)
  const arrive = new Date(dto.arrivalTime)
  const kind = dto.tripType === 'Sightseeing' ? 'sightseeing' : 'transit'
  return {
    id: dto.id,
    kind,
    vesselCode: tripReference(dto.id),
    vesselName: dto.routeName,
    lineLabel: dto.routeName,
    variant: kind === 'sightseeing' ? 'sunset' : 'default',
    departTime: formatClock(depart),
    arriveTime: formatClock(arrive),
    originGate: '',
    destinationGate: '',
    durationMins: dto.durationMinutes,
    crossingKind: kind === 'sightseeing' ? t('Sightseeing') : t('Regular'),
    direct: false,
    crossingCaption: dto.routeName,
    seatsCount: dto.availableSeatsCount,
    seatsLow: dto.availableSeatsCount <= LOW_SEATS_THRESHOLD,
    band: timeBandOf(depart.getHours()),
    amenities: [],
    fareVnd: dto.basePrice,
    fareLabel: kind === 'sightseeing' ? t('Experience Fare') : t('Standard Fare'),
  }
}
