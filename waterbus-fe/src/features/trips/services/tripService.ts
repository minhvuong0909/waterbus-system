import { withDelay } from '../../../mocks/delay'
import {
  BOARDING_STEPS,
  DATE_OPTIONS,
  NETWORK_ROUTES,
  ONBOARD_AMENITIES,
  PIER_OPTIONS,
  TRIPS,
} from '../../../mocks/tripSearch'
import { localize, t } from '../../../i18n'
import { addMinutes } from '../../../utils/time'
import { formatDateLabel } from '../utils'
import type { DateOption, NetworkRoute, PierOption, SearchQuery, Trip, TripDetail } from '../types'

// Mock service layer: swap for real API calls later without touching the UI.
// `withDelay` returns the data translated into the active language, like a localized API.
export function getPierOptions(): Promise<PierOption[]> {
  return withDelay(PIER_OPTIONS)
}

export function getNetworkRoutes(): Promise<NetworkRoute[]> {
  return withDelay(NETWORK_ROUTES)
}

export function getDateOptions(): Promise<DateOption[]> {
  return withDelay(DATE_OPTIONS)
}

/** Results ignore the pier pair in this mock; the same day's departures are always returned. */
export function getTrips(query: SearchQuery): Promise<Trip[]> {
  void query
  return withDelay(TRIPS)
}

const findPier = (id: string) => PIER_OPTIONS.find((p) => p.id === id) ?? PIER_OPTIONS[0]

/** Falls back to the first trip and the default piers, so the page never errors. */
export function getTripDetail(tripId: string | null, query: SearchQuery): Promise<TripDetail> {
  const trip = TRIPS.find((item) => item.id === tripId) ?? TRIPS[0]
  // Translate the pieces used to build sentences below; the rest is translated by `withDelay`.
  const origin = localize(findPier(query.originId))
  const destination = localize(findPier(query.destinationId))
  const date = localize(DATE_OPTIONS.find((d) => d.id === query.dateId) ?? DATE_OPTIONS[2])

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
