import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { withDelay } from '../../../mocks/delay'
import {
  BOARDING_STEPS,
  NETWORK_ROUTE_SEEDS,
  ONBOARD_AMENITIES,
  PIER_OPTIONS,
  TRIPS,
} from '../../../mocks/tripSearch'
import { localize, t } from '../../../i18n'
import { apiRequest } from '../../../services/apiClient'
import { addMinutes } from '../../../utils/time'
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
} from '../utils'

/** `GET /stations`: the active piers in river order. */
export async function getPierOptions(): Promise<PierOption[]> {
  if (USE_MOCK) return withDelay(PIER_OPTIONS)
  const stations = await apiRequest<StationDto[]>(API_ENDPOINTS.stations)
  return stations.map(toPierOption)
}

// Mock: the backend has no route list endpoint yet. Seeds are matched to the real stations by
// code, so a route is only offered when both of its piers exist.
export async function getNetworkRoutes(): Promise<NetworkRoute[]> {
  const piers = await getPierOptions()
  const idOf = (code: string) => piers.find((p) => p.code === code)?.id
  const routes = NETWORK_ROUTE_SEEDS.flatMap(({ originCode, destinationCode, ...route }) => {
    const originId = idOf(originCode)
    const destinationId = idOf(destinationCode)
    return originId && destinationId ? [{ ...route, originId, destinationId }] : []
  })
  return withDelay(routes, 0)
}

/** Today and the following days. The backend has no per-day summary (fares, trip counts). */
export function getDateOptions(): Promise<DateOption[]> {
  return Promise.resolve(buildDateOptions())
}

/** `GET /trips/search` for the selected piers and day. */
export async function getTrips(query: SearchQuery): Promise<Trip[]> {
  if (USE_MOCK) return withDelay(TRIPS)
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
 * Detail page data. The backend has no trip detail endpoint yet (roadmap P1-BE-05), so it is built
 * from the trip chosen in the results list, plus static content. A trip that was not chosen in
 * this session (direct URL, or picked on the home page) falls back to the mock trips.
 */
export async function getTripDetail(
  selectedTrip: Trip | null,
  tripId: string | null,
  query: SearchQuery,
): Promise<TripDetail> {
  const trip = selectedTrip ?? TRIPS.find((item) => item.id === tripId) ?? TRIPS[0]
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
    dateFull: date.fullLabel,
    dateShort: date.label,
    timeline: [
      {
        time: addMinutes(trip.departTime, -15),
        title: t('Arrive at {pier}', { pier: origin.pierName }),
        description: t(
          'Plan to arrive at the terminal pier a few minutes before scheduled departure.',
        ),
        tone: 'teal',
      },
      {
        time: addMinutes(trip.departTime, -5),
        title: t('Boarding'),
        description: t('Vessel boarding begins for ticketed passengers.'),
        tone: 'amber',
      },
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
