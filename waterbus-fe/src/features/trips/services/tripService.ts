import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { BOARDING_STEPS, ONBOARD_AMENITIES } from '../../../content/tripDetail'
import { DEFAULT_SEARCH_QUERY } from '../searchDefaults'
import { localize, t } from '../../../i18n'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { formatClock, toVietnamDate } from '../../../utils/time'
import { getLowestFares } from './fareService'
import type {
  DateOption,
  NetworkRoute,
  PierOption,
  SearchQuery,
  StationDto,
  Trip,
  TripDetail,
  TripDto,
} from '../types'
import {
  buildDateOptions,
  describeDate,
  formatDateLabel,
  pickPiers,
  toPierOption,
  toTrip,
  isDeparted,
} from '../utils'
import type { TripDetailDto, TripSeatMapDto } from '../../booking/types'
import { pickJourney } from '../../booking/utils'

/** The piers change rarely and one page asks for them several times, so they are kept this long. */
const PIER_CACHE_MS = 60_000
let pierCache: { piers: Promise<PierOption[]>; at: number } | undefined

/** Forgets the cached piers, e.g. after an Admin changed a station. */
export function clearPierCache() {
  pierCache = undefined
}

/** `GET /stations`: the active piers in river order. Requests in flight and recent results are shared. */
export function getPierOptions(): Promise<PierOption[]> {
  if (pierCache && Date.now() - pierCache.at < PIER_CACHE_MS) return pierCache.piers
  const piers = apiRequest<StationDto[]>(API_ENDPOINTS.stations).then((stations) => stations.map(toPierOption))
  const entry = { piers, at: Date.now() }
  pierCache = entry
  // A failure is not kept: the next call asks again.
  piers.catch(() => {
    if (pierCache === entry) pierCache = undefined
  })
  return piers
}

/**
 * The backend has no route list endpoint yet, so the corridor is derived from the real stations:
 * the first and the last pier in river order, offered in both directions, with the lowest
 * Commuter fare from `GET /fares`. No duration or distance is shown, which no API provides.
 */
export async function getNetworkRoutes(): Promise<NetworkRoute[]> {
  const [piers, fares] = await Promise.all([
    getPierOptions(),
    getLowestFares().catch(() => undefined),
  ])
  if (piers.length < 2) return []
  const first = piers[0]
  const last = piers[piers.length - 1]
  const corridor = (from: PierOption, to: PierOption, badge: string): NetworkRoute => ({
    id: `${from.code}-${to.code}`,
    originId: from.id,
    destinationId: to.id,
    badge,
    summary: t('River route between {from} and {to}', { from: from.shortName, to: to.shortName }),
    fromFareVnd: fares?.Commuter,
  })
  return localize([corridor(first, last, 'Main Corridor'), corridor(last, first, 'Return')])
}

/** Today and the following days. The backend has no per-day summary (fares, trip counts). */
export function getDateOptions(): Promise<DateOption[]> {
  return Promise.resolve(buildDateOptions())
}

/** `GET /trips/search` for the selected piers and day. */
export async function getTrips(query: SearchQuery): Promise<Trip[]> {
  const dtos = await apiRequest<TripDto[]>(API_ENDPOINTS.trips.search, {
    query: {
      departureStationId: query.originId,
      arrivalStationId: query.destinationId,
      departureDate: query.dateId,
    },
  })
  return dtos.map(toTrip)
}

/**
 * Số chuyến còn bán (còn ghế, chưa khởi hành) của từng ngày cho cặp bến đang chọn, để thanh chọn
 * ngày cho thấy ngày nào có chuyến. Một ngày lỗi thì bỏ qua (không có số), không làm hỏng trang.
 */
export async function getDayTripCounts(
  query: SearchQuery,
  dates: DateOption[],
): Promise<Record<string, number>> {
  const entries = await Promise.all(
    dates.map(async (date) => {
      try {
        const trips = await getTrips({ ...query, dateId: date.id })
        const bookable = trips.filter((trip) => trip.seatsCount > 0 && !isDeparted(trip, date.id))
        return [date.id, bookable.length] as const
      } catch {
        return [date.id, -1] as const
      }
    }),
  )
  return Object.fromEntries(entries.filter(([, count]) => count >= 0))
}

/** Minutes since midnight of an "HH:mm" time. */
const minutesOf = (time: string) => {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

/**
 * The next departures today on the main corridor (first to last pier), with the search they came
 * from. Departures that already left are dropped.
 */
export async function getUpcomingTrips(
  limit: number,
  pair: Pick<SearchQuery, 'originId' | 'destinationId'> = DEFAULT_SEARCH_QUERY,
): Promise<{
  trips: Trip[]
  piers: PierOption[]
  search: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>
  /** True when today has no departure left and the trips are tomorrow's. */
  tomorrow: boolean
}> {
  const piers = await getPierOptions()
  const search = { ...pickPiers(piers, pair), dateId: toVietnamDate(new Date()) }
  const all = await getTrips({ ...DEFAULT_SEARCH_QUERY, ...search })
  const now = new Date()
  const bookable = (trip: Trip) => trip.seatsCount > 0
  const today = all
    .filter((trip) => minutesOf(trip.departTime) >= now.getHours() * 60 + now.getMinutes())
    .filter(bookable)
    .slice(0, limit)
  if (today.length > 0) return { trips: today, piers, search, tomorrow: false }

  // Hôm nay không còn chuyến: cho khách thấy lịch ngày mai thay vì một khung trống.
  const nextDay = { ...search, dateId: toVietnamDate(new Date(now.getTime() + 24 * 60 * 60 * 1000)) }
  const next = (await getTrips({ ...DEFAULT_SEARCH_QUERY, ...nextDay })).filter(bookable).slice(0, limit)
  return next.length > 0
    ? { trips: next, piers, search: nextDay, tomorrow: true }
    : { trips: [], piers, search, tomorrow: false }
}

/**
 * Detail page data from `GET /trips/{id}` and `GET /trips/{id}/seats`, plus static content. Without
 * a backend trip id the trip can only come from the search results; otherwise it is not found.
 */
export async function getTripDetail(
  selectedTrip: Trip | null,
  tripId: string | null,
  query: SearchQuery,
): Promise<TripDetail> {
  let trip = selectedTrip
  // Check-in window of the boarding stop from the backend; null while operations has not set it.
  let checkIn: { openAt: string | null; closeAt: string | null } | null = null
  if (tripId) {
    const raw = await apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId))
    const journey = pickJourney(raw.stops, query.originId, query.destinationId)
    if (!journey) throw new ApiError(409, 'Chuyến không phục vụ đoạn bến đã chọn. Hãy tìm chuyến lại.')
    const seats = await apiRequest<TripSeatMapDto>(API_ENDPOINTS.trips.seats(tripId), {
      query: { boardingCallId: journey.boarding.callId, disembarkingCallId: journey.disembarking.callId },
    })
    const prices = seats.seats.flatMap((seat) => seat.price === null ? [] : [seat.price])
    checkIn = { openAt: journey.boarding.checkInOpenAt ?? null, closeAt: journey.boarding.checkInCloseAt ?? null }
    const departureTime = journey.boarding.estimatedDepartureTime ?? raw.departureTime
    const arrivalTime = journey.disembarking.estimatedArrivalTime ?? raw.arrivalTime
    trip = toTrip({ id: raw.tripId, routeName: raw.routeName, tripType: raw.tripType,
      departureStationName: journey.boarding.stationName, arrivalStationName: journey.disembarking.stationName,
      departureTime, arrivalTime,
      durationMinutes: Math.round((Date.parse(arrivalTime) - Date.parse(departureTime)) / 60000),
      availableSeatsCount: seats.availableSeatsCount, basePrice: prices.length ? Math.min(...prices) : 0 })
    trip.vesselCode = raw.boat?.code ?? trip.vesselCode
    trip.vesselName = raw.boat?.name ?? raw.routeName
    query = { ...query, dateId: new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(departureTime)) }
  }
  if (!trip) throw new ApiError(404, t('This trip is no longer available. Please search again.'))
  const piers = await getPierOptions()
  const picked = pickPiers(piers, query)
  const find = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  // Translate the pieces used to build sentences below; the rest is translated by `localize` at the end.
  const origin = localize(find(picked.originId))
  const destination = localize(find(picked.destinationId))
  const date = describeDate(query.dateId)

  const detail: TripDetail = {
    ...trip,
    originPierLabel: origin.name,
    destinationPierLabel: destination.name,
    originPierName: origin.pierName,
    destinationPierName: destination.pierName,
    originShortName: origin.shortName,
    destinationShortName: destination.shortName,
    originDistrict: origin.district,
    destinationDistrict: destination.district,
    dateLabel: formatDateLabel(date),
    dateId: date.id,
    dateFull: date.fullLabel,
    dateShort: date.label,
    timeline: [
      // The boarding stop's check-in window; no row at all while it is not configured.
      ...(checkIn?.openAt
        ? [{
            time: formatClock(new Date(checkIn.openAt)),
            title: t('Check-in opens at {pier}', { pier: origin.pierName }),
            description: t('Show your QR ticket to the staff at the pier.'),
            tone: 'teal' as const,
          }]
        : []),
      ...(checkIn?.closeAt
        ? [{
            time: formatClock(new Date(checkIn.closeAt)),
            title: t('Check-in closes'),
            description: t('Passengers who have not checked in by now cannot board.'),
            tone: 'amber' as const,
          }]
        : []),
      {
        time: trip.departTime,
        title: t('Departure'),
        description: t('{code} departs {pier} across the river corridor.', {
          code: trip.vesselCode,
          pier: origin.pierName,
        }),
        tone: 'teal',
      },
      {
        time: trip.arriveTime,
        title: t('Arrival at {pier}', { pier: destination.pierName }),
        description: t('Scheduled docking at {pier}.', { pier: destination.pierName }),
        tone: 'dark',
      },
    ],
    amenityDetails: ONBOARD_AMENITIES,
    boardingSteps: BOARDING_STEPS.map((step) => ({
      ...step,
      // The {origin} placeholder survives translation, so it is filled in afterwards.
      description: t(step.description, { origin: origin.pierName }),
    })),
  }
  return localize(detail)
}
