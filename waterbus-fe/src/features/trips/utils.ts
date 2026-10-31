import { numberLocale, t } from '../../i18n'
import { formatClock, toVietnamDate } from '../../utils/time'
import type {
  CorridorMap,
  DateOption,
  PierOption,
  SearchQuery,
  StationDto,
  TimeBand,
  Trip,
  TripDto,
  TripStopDto,
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
  if (trip.seatsCount <= 0) return t('Sold out')
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
    latitude: station.latitude,
    longitude: station.longitude,
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

/** The search endpoint sends no fare (the real fares belong to each seat class), so 0 means "not known yet". */
export const hasFare = (amountVnd: number): boolean => amountVnd > 0

/** Short reference for a trip, the first 8 characters of its id (e.g. "3F2A9C1B"). */
export function tripReference(id: string): string {
  return id.replace(/-/g, '').slice(0, 8).toUpperCase()
}

/** Date option for a `YYYY-MM-DD` id, formatted for the active language. */
export function describeDate(id: string, today = toVietnamDate(new Date())): DateOption {
  const [year, month, day] = id.split('-').map(Number)
  // Noon UTC keeps the same calendar day in Vietnam (UTC+7) whatever the browser's time zone.
  const date = new Date(Date.UTC(year, month - 1, day, 12))
  const locale = numberLocale()
  const zone = 'Asia/Ho_Chi_Minh'
  return {
    id,
    weekday: date.toLocaleDateString(locale, { weekday: 'short', timeZone: zone }),
    label: date.toLocaleDateString(locale, { month: 'short', day: 'numeric', timeZone: zone }),
    fullLabel: date.toLocaleDateString(locale, { year: 'numeric', month: 'short', day: 'numeric', timeZone: zone }),
    isToday: id === today,
  }
}

/** The next `days` days starting today. */
export function buildDateOptions(days = SEARCH_WINDOW_DAYS): DateOption[] {
  const [year, month, day] = toVietnamDate(new Date()).split('-').map(Number)
  return Array.from({ length: days }, (_, offset) =>
    describeDate(toVietnamDate(new Date(Date.UTC(year, month - 1, day + offset, 12)))),
  )
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
    // basePrice is the lowest fare of the trip type; seats of other classes cost more.
    fareLabel: t('Fares from'),
  }
}

/**
 * Chuyến đã rời bến: ngày đã qua, hoặc hôm nay mà giờ khởi hành không còn ở phía trước. Chuyến loại
 * này không còn bán được (BR-SRCH-02, BR-TIME-01).
 */
export function isDeparted(trip: Pick<Trip, 'departTime'>, dateId: string, now = new Date()): boolean {
  const today = toVietnamDate(now)
  if (dateId !== today) return dateId < today
  const [hours, minutes] = trip.departTime.split(':').map(Number)
  return hours * 60 + minutes <= now.getHours() * 60 + now.getMinutes()
}

/**
 * The boarding and alighting stops for the chosen piers: the first stop at the origin pier that
 * allows boarding, followed later in the trip by a stop at the destination that allows alighting.
 * Uses the trip's real stops, never the pier order (a loop trip can call at one pier twice).
 */
export function pickJourney(
  stops: TripStopDto[],
  originId: string,
  destinationId: string,
): { boarding: TripStopDto; disembarking: TripStopDto } | null {
  const usable = stops.filter((stop) => stop.status !== 'Skipped')
  const sameStation = (a: string, b: string) => a.toLowerCase() === b.toLowerCase()
  const boardings = usable
    .filter((stop) => stop.canBoard && sameStation(stop.stationId, originId))
    .sort((a, b) => a.visitOrder - b.visitOrder)
  for (const boarding of boardings) {
    const disembarking = usable
      .filter(
        (stop) =>
          stop.canAlight &&
          sameStation(stop.stationId, destinationId) &&
          stop.visitOrder > boarding.visitOrder,
      )
      .sort((a, b) => a.visitOrder - b.visitOrder)[0]
    if (disembarking) return { boarding, disembarking }
  }
  return null
}

/** The corridor shown on the maps: the piers at their coordinates. */
export function buildCorridorMap(piers: PierOption[]): CorridorMap {
  const placed = piers.map((pier) => ({
    id: pier.id,
    name: pier.name,
    subtitle: pier.district,
    code: pier.code,
    address: pier.subtitle,
    latitude: pier.latitude,
    longitude: pier.longitude,
  }))
  return { piers: placed }
}
