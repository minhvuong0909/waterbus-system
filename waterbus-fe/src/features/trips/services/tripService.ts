import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { DEFAULT_SEARCH_QUERY } from '../searchDefaults'
import { localize, t } from '../../../i18n'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { createRequestCache } from '../../../services/requestCache'
import { formatClock, minutesOfClock, toVietnamDate, vietnamMinutesOfDay } from '../../../utils/time'
import type {
  DateOption,
  PierOption,
  SearchQuery,
  StationDto,
  Trip,
  TripDetail,
  TripDto,
} from '../types'
import {
  buildDateOptions,
  chronological,
  describeDate,
  formatDateLabel,
  pickPiers,
  toPierOption,
  toTrip,
  isDeparted,
} from '../utils'
import type { TripDetailDto, TripSeatMapDto } from '../types'
import { pickJourney } from '../utils'

/** The piers change rarely and one page asks for them several times. */
const pierCache = createRequestCache<PierOption[]>(60_000)
/** A page asks for the same day from several places (list, day strip, home); seats change, so briefly. */
const tripSearchCache = createRequestCache<Trip[]>(15_000)

/**
 * A booking page reads the same trip and seat map from two places at once (the trip summary and the
 * seat map). The requests in flight are shared; one second is short enough that every new step and
 * every retry still reads fresh availability.
 */
const SHARED_READ_MS = 1_000
const tripReadCache = createRequestCache<TripDetailDto>(SHARED_READ_MS)
const seatReadCache = createRequestCache<TripSeatMapDto>(SHARED_READ_MS)

/** `GET /trips/{id}`. */
export function readTrip(tripId: string): Promise<TripDetailDto> {
  return tripReadCache.get(tripId, () => apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId)))
}

/** `GET /trips/{id}/seats` for the segment between two stop calls. */
export function readTripSeats(
  tripId: string,
  boardingCallId: string,
  disembarkingCallId: string,
): Promise<TripSeatMapDto> {
  return seatReadCache.get(`${tripId}|${boardingCallId}|${disembarkingCallId}`, () =>
    apiRequest<TripSeatMapDto>(API_ENDPOINTS.trips.seats(tripId), {
      query: { boardingCallId, disembarkingCallId },
    }),
  )
}

/** Forgets the shared trip and seat reads, so the next read asks the backend again. */
export function clearTripReads() {
  tripReadCache.clear()
  seatReadCache.clear()
}

/** Forgets the cached piers, e.g. after an Admin changed a station. */
export function clearPierCache() {
  pierCache.clear()
}

/** `GET /stations`: the active piers in river order. */
export function getPierOptions(): Promise<PierOption[]> {
  return pierCache.get('all', () =>
    apiRequest<StationDto[]>(API_ENDPOINTS.stations).then((stations) => stations.map(toPierOption)),
  )
}

/** Today and the following days. The backend has no per-day summary (fares, trip counts). */
export function getDateOptions(): Promise<DateOption[]> {
  return Promise.resolve(buildDateOptions())
}

/** `GET /trips/search` for the selected piers and day. */
export function getTrips(query: SearchQuery): Promise<Trip[]> {
  const key = `${query.originId}|${query.destinationId}|${query.dateId}`
  return tripSearchCache.get(key, async () => {
    const dtos = await apiRequest<TripDto[]>(API_ENDPOINTS.trips.search, {
      query: {
        departureStationId: query.originId,
        arrivalStationId: query.destinationId,
        departureDate: query.dateId,
      },
    })
    return dtos.map(toTrip)
  })
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
    .filter((trip) => minutesOfClock(trip.departTime) >= vietnamMinutesOfDay(now))
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
 * True when the backend still sells seats on the trip (`canBook` of `GET /trips/{id}`). The search
 * result has no sales state, so a list that invites to book checks it first (BR-SRCH-02).
 */
export async function isTripOnSale(tripId: string): Promise<boolean> {
  return (await readTrip(tripId)).canBook
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
  // Sales state of the backend; a trip that only comes from the search results is taken as on sale.
  let salesOpen = true
  // Mốc thời gian thật để xếp lộ trình theo thứ tự; trip chỉ từ kết quả tìm kiếm thì giữ thứ tự sẵn có.
  let departAt = Number.NaN
  let arriveAt = Number.NaN
  if (tripId) {
    const raw = await readTrip(tripId)
    const journey = pickJourney(raw.stops, query.originId, query.destinationId)
    if (!journey) throw new ApiError(409, 'Chuyến không phục vụ đoạn bến đã chọn. Hãy tìm chuyến lại.')
    const seats = await readTripSeats(tripId, journey.boarding.callId, journey.disembarking.callId)
    const prices = seats.seats.flatMap((seat) => seat.price === null ? [] : [seat.price])
    salesOpen = raw.canBook && seats.canBook
    checkIn = { openAt: journey.boarding.checkInOpenAt ?? null, closeAt: journey.boarding.checkInCloseAt ?? null }
    const departureTime = journey.boarding.estimatedDepartureTime ?? raw.departureTime
    const arrivalTime = journey.disembarking.estimatedArrivalTime ?? raw.arrivalTime
    departAt = Date.parse(departureTime)
    arriveAt = Date.parse(arrivalTime)
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
    salesOpen,
    dateId: date.id,
    dateFull: date.fullLabel,
    dateShort: date.label,
    timeline: chronological([
      // The boarding stop's check-in window; no row at all while it is not configured.
      ...(checkIn?.openAt
        ? [{
            at: Date.parse(checkIn.openAt),
            time: formatClock(new Date(checkIn.openAt)),
            title: t('Check-in opens at {pier}', { pier: origin.pierName }),
            description: t('Show your QR ticket to the staff at the pier.'),
            tone: 'teal' as const,
          }]
        : []),
      ...(checkIn?.closeAt
        ? [{
            at: Date.parse(checkIn.closeAt),
            time: formatClock(new Date(checkIn.closeAt)),
            title: t('Check-in closes'),
            description: t('Passengers who have not checked in by now cannot board.'),
            tone: 'amber' as const,
          }]
        : []),
      {
        at: departAt,
        time: trip.departTime,
        title: t('Departure'),
        description: t('{code} departs {pier} across the river corridor.', {
          code: trip.vesselCode,
          pier: origin.pierName,
        }),
        tone: 'teal',
      },
      {
        at: arriveAt,
        time: trip.arriveTime,
        title: t('Arrival at {pier}', { pier: destination.pierName }),
        description: t('Scheduled docking at {pier}.', { pier: destination.pierName }),
        tone: 'dark',
      },
    ]),
  }
  return localize(detail)
}

/** One stop of a journey, with the time the boat is expected there (Vietnam clock, "HH:mm"). */
export interface JourneyStop {
  callId: string
  stationName: string
  time: string
}

/**
 * The stops a traveler passes from the origin pier to the destination pier of a trip, from
 * `GET /trips/{id}`. The search result only has the first departure and the last arrival of the
 * whole trip, so this is where the times at the traveler's own piers come from.
 */
export async function getJourneyStops(
  tripId: string,
  originId: string,
  destinationId: string,
): Promise<JourneyStop[]> {
  const trip = await apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId))
  const journey = pickJourney(trip.stops, originId, destinationId)
  if (!journey) return []
  return trip.stops
    .filter(
      (stop) =>
        stop.status !== 'Skipped' &&
        stop.visitOrder >= journey.boarding.visitOrder &&
        stop.visitOrder <= journey.disembarking.visitOrder,
    )
    .sort((a, b) => a.visitOrder - b.visitOrder)
    .map((stop) => {
      // The boat leaves the boarding pier and arrives at every pier after it.
      const instant =
        stop.callId === journey.boarding.callId
          ? stop.estimatedDepartureTime ?? stop.estimatedArrivalTime
          : stop.estimatedArrivalTime ?? stop.estimatedDepartureTime
      return {
        callId: stop.callId,
        stationName: stop.stationName,
        time: instant ? formatClock(new Date(instant)) : '',
      }
    })
}
