import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { withDelay } from '../../../mocks/delay'
import {
  BOARDING_STEPS,
  DEFAULT_SEARCH_QUERY,
  NETWORK_ROUTE_SEEDS,
  ONBOARD_AMENITIES,
  PIER_OPTIONS,
  buildTrips,
} from '../../../mocks/tripSearch'
import { localize, t } from '../../../i18n'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { addMinutes, formatClock, toVietnamDate } from '../../../utils/time'
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
  if (USE_MOCK) return withDelay(PIER_OPTIONS)
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

// The backend has no route list endpoint yet, so the corridors are seeds matched to the real
// stations by code (a route is only offered when both of its piers exist). With the real backend the
// fare comes from `GET /fares` and the summary carries no invented duration or distance.
export async function getNetworkRoutes(): Promise<NetworkRoute[]> {
  const [piers, fares] = await Promise.all([
    getPierOptions(),
    USE_MOCK ? Promise.resolve(undefined) : getLowestFares().catch(() => undefined),
  ])
  const idOf = (code: string) => piers.find((p) => p.code === code)?.id
  const routes = NETWORK_ROUTE_SEEDS.flatMap(({ originCode, destinationCode, ...route }) => {
    const originId = idOf(originCode)
    const destinationId = idOf(destinationCode)
    if (!originId || !destinationId) return []
    return [USE_MOCK
      ? { ...route, originId, destinationId }
      : { ...route, originId, destinationId, summary: route.liveSummary, fromFareVnd: fares?.Commuter }]
  })
  return withDelay(routes, 0)
}

/** Today and the following days. The backend has no per-day summary (fares, trip counts). */
export function getDateOptions(): Promise<DateOption[]> {
  return Promise.resolve(buildDateOptions())
}

/** `GET /trips/search` for the selected piers and day. */
export async function getTrips(query: SearchQuery): Promise<Trip[]> {
  if (USE_MOCK) return withDelay(buildTrips(query))
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
 * Detail page data. The backend has no trip detail endpoint yet (roadmap P1-BE-05), so it is built
 * from the trip chosen in the results list, plus static content. A trip that was not chosen in
 * this session (for example after a page reload) is only possible with mock data; with the real
 * backend it is reported as not found.
 */
export async function getTripDetail(
  selectedTrip: Trip | null,
  tripId: string | null,
  query: SearchQuery,
): Promise<TripDetail> {
  const mockTrips = USE_MOCK ? buildTrips() : []
  let trip = selectedTrip ?? mockTrips.find((item) => item.id === tripId) ?? mockTrips[0]
  // Check-in window of the boarding stop from the backend; null while operations has not set it.
  let checkIn: { openAt: string | null; closeAt: string | null } | null = null
  if (!USE_MOCK && tripId) {
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
  // Without the backend's trip detail endpoint, a real trip can only come from the search results.
  if (!trip) throw new ApiError(404, t('This trip is no longer available. Please search again.'))
  const piers = await getPierOptions()
  const picked = pickPiers(piers, query)
  const find = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  // Translate the pieces used to build sentences below; the rest is translated by `withDelay`.
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
      // Mock: example times. Real backend: the boarding stop's check-in window, or no row at all
      // while it is not configured (no invented "15 minutes before").
      ...(USE_MOCK
        ? [
            {
              time: addMinutes(trip.departTime, -15),
              title: t('Arrive at {pier}', { pier: origin.pierName }),
              description: t('Plan to arrive at the terminal pier a few minutes before scheduled departure.'),
              tone: 'teal' as const,
            },
            {
              time: addMinutes(trip.departTime, -5),
              title: t('Boarding'),
              description: t('Vessel boarding begins for ticketed passengers.'),
              tone: 'amber' as const,
            },
          ]
        : [
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
          ]),
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
  return withDelay(detail)
}
