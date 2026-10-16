import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { FleetBoat, LiveBoatPosition, WeatherAlert, WeatherCheckResult } from '../types'

// Live operations backed by the real API: boats, boat tracking and weather incidents.
// Realtime updates for the same data come from the boat-tracking hub (see useFleetHub).

interface Paged<T> {
  items: T[]
  totalCount: number
}

interface BoatLatestPositionDto {
  boatId: string
  tripId: string | null
  latitude: number
  longitude: number
  speedKnots: number | null
  observedAt: string
}

interface IncidentSummaryDto {
  id: string
  severity: string | null
  description: string | null
  occurredAt: string | null
  affectedTripCount: number
}

/** Payload of the `boatPosition` hub event (note `lat` / `lng`, unlike the REST snapshot). */
export interface BoatPositionEvent {
  boatId: string
  tripId: string | null
  lat: number
  lng: number
  speedKnots: number | null
  observedAt: string
}

/** Payload of the `weatherAlert` hub event. */
export interface WeatherAlertEvent {
  incidentId: string
  severity: string | null
  routeId: string | null
  message: string
  occurredAt: string
}

export async function getFleetBoats(): Promise<FleetBoat[]> {
  const page = await apiRequest<Paged<FleetBoat>>(API_ENDPOINTS.boats, { query: { pageSize: 100 } })
  return page.items.filter((b) => b.isActive)
}

export const getLatestBoatPositions = () => apiRequest<BoatLatestPositionDto[]>(API_ENDPOINTS.tracking.boats)

export function fromPositionEvent(event: BoatPositionEvent): LiveBoatPosition {
  return {
    boatId: event.boatId,
    tripId: event.tripId,
    latitude: event.lat,
    longitude: event.lng,
    speedKnots: event.speedKnots,
    observedAt: event.observedAt,
  }
}

const levelOf = (severity: string | null): 1 | 2 => (severity === 'High' ? 2 : 1)

/** Open weather incidents created by the weather guard, newest first. */
export async function getOpenWeatherAlerts(): Promise<WeatherAlert[]> {
  const page = await apiRequest<Paged<IncidentSummaryDto>>(API_ENDPOINTS.incidents.list, {
    query: { status: 'Open', incidentType: 'Weather', pageSize: 20 },
  })
  return page.items.map((i) => ({
    id: i.id,
    level: levelOf(i.severity),
    message: i.description ?? '',
    issuedAt: i.occurredAt ?? new Date().toISOString(),
    affectedTripCount: i.affectedTripCount,
  }))
}

export const runWeatherCheck = () =>
  apiRequest<WeatherCheckResult>(API_ENDPOINTS.incidents.weatherCheck, { method: 'POST' })

/** Knots (BE) to km/h (UI). */
export const knotsToKmh = (knots: number | null) => (knots == null ? null : Math.round(knots * 1.852 * 10) / 10)
