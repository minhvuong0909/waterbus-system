import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type {
  AdminTrip,
  AdminTripStatus,
  AdminTripType,
  LiveBoatPosition,
  OperationLog,
  OperationType,
  WeatherAlert,
  WeatherCheckResult,
} from '../types'

// Operations backed by the real API: trips and their status, boat tracking and incidents.
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
  pendingDecisionCount?: number
  routeCode?: string | null
  source?: string
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

/** Payload of the `tripStatusChanged` hub event. */
export interface TripStatusEvent {
  tripId: string
  boatId: string | null
  status: AdminTripStatus
  previousStatus: AdminTripStatus
  operation: OperationType
  changedAt: string
}

/** Payload of the `incidentRaised` hub event (a captain's SOS). */
export interface IncidentRaisedEvent {
  incidentId: string
  incidentType: string
  severity: string | null
  tripId: string | null
  boatId: string | null
  message: string
  occurredAt: string
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

interface IncidentSummaryFull extends IncidentSummaryDto {
  incidentType: string
}

interface IncidentDetailDto {
  affectedTrips: {
    tripId: string
    operationDecision: string | null
    departureTime?: string
    /** The backend sends the enum number here (1 = Scheduled … 8 = Terminated). */
    tripStatus?: number | string
    decisionAt?: string | null
  }[]
}

const TRIP_STATUS_BY_NUMBER: AdminTripStatus[] = [
  'Scheduled', 'Boarding', 'EnRoute', 'Suspended', 'Arrived', 'Completed', 'Cancelled', 'Terminated',
]

/** A trip status sent as the enum number or its name. */
function tripStatusOf(value: number | string | undefined): AdminTripStatus | undefined {
  if (typeof value === 'number') return TRIP_STATUS_BY_NUMBER[value - 1]
  return TRIP_STATUS_BY_NUMBER.find((status) => status === value)
}

const KINDS = ['Weather', 'Safety', 'Technical', 'Other'] as const

/**
 * Open incidents, newest first: weather alerts from the weather guard and captains' SOS reports.
 * `withTrips` also loads the trips each incident affects and what the admin decided for them.
 */
export async function getOpenIncidents(withTrips = false): Promise<WeatherAlert[]> {
  const page = await apiRequest<Paged<IncidentSummaryFull>>(API_ENDPOINTS.incidents.list, {
    query: { status: 'Open', pageSize: 20 },
  })
  return Promise.all(
    page.items.map(async (i): Promise<WeatherAlert> => {
      const trips = withTrips
        ? (await apiRequest<IncidentDetailDto>(API_ENDPOINTS.incidents.detail(i.id))).affectedTrips.map((t) => ({
            tripId: t.tripId,
            decision: t.operationDecision,
            departureTime: t.departureTime,
            tripStatus: tripStatusOf(t.tripStatus),
            decisionAt: t.decisionAt ?? null,
          }))
        : undefined
      return {
        id: i.id,
        level: levelOf(i.severity),
        message: i.description ?? '',
        issuedAt: i.occurredAt ?? new Date().toISOString(),
        kind: KINDS.find((k) => k === i.incidentType) ?? 'Other',
        affectedTripCount: i.affectedTripCount,
        pendingDecisionCount: i.pendingDecisionCount,
        routeCode: i.routeCode ?? null,
        source: i.source,
        trips,
      }
    }),
  )
}

export const runWeatherCheck = () =>
  apiRequest<WeatherCheckResult>(API_ENDPOINTS.incidents.weatherCheck, { method: 'POST' })

export const resolveIncident = (incidentId: string) =>
  apiRequest<{ incidentId: string; status: string }>(API_ENDPOINTS.incidents.resolve(incidentId), { method: 'POST' })

/** Knots (BE) to km/h (UI). */
export const knotsToKmh = (knots: number | null) => (knots == null ? null : Math.round(knots * 1.852 * 10) / 10)

// ---- Trips for operations ---------------------------------------------------

interface TripOperationItemDto {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  boatId: string | null
  boatCode: string | null
  tripType: AdminTripType
  status: AdminTripStatus
  departureTime: string
  arrivalTime: string
  allowedOperations: OperationType[]
}

/** Trips from 12 hours ago to 7 days ahead, with the operations the backend allows on each right now. */
export async function getOperationTrips(): Promise<AdminTrip[]> {
  const items = await apiRequest<TripOperationItemDto[]>(API_ENDPOINTS.trips.operations)
  return items.map((t) => ({
    id: t.id,
    routeId: t.routeId,
    boatId: t.boatId ?? '',
    departureTime: t.departureTime,
    arrivalTime: t.arrivalTime,
    tripType: t.tripType,
    status: t.status,
    routeCode: t.routeCode,
    routeName: t.routeName,
    boatCode: t.boatCode,
    allowedOperations: t.allowedOperations,
  }))
}

interface TripOperationEventDto {
  id: string
  tripId: string
  action: string
  note: string | null
  occurredAt: string
  actedBy: string | null
}

export async function getOperationLogs(): Promise<OperationLog[]> {
  const events = await apiRequest<TripOperationEventDto[]>(API_ENDPOINTS.trips.operationEvents, { query: { take: 50 } })
  return events.map((e) => ({
    id: e.id,
    type: e.action as OperationType,
    tripId: e.tripId,
    reason: e.note ?? '',
    by: e.actedBy ?? '—',
    at: e.occurredAt,
  }))
}

/**
 * Changes a trip's status in the backend. The backend checks who may do what: a captain can open boarding,
 * start and arrive on their own boat; an admin decides everything else. Pass `incidentId` to record the
 * decision against an incident. A captain's SOS creates an incident and leaves the trip as it is.
 */
export async function operateTrip(
  tripId: string,
  type: OperationType,
  reason: string,
  incidentId?: string,
): Promise<{ status: AdminTripStatus | null }> {
  if (type === 'SOS') {
    await apiRequest(API_ENDPOINTS.trips.sos(tripId), { method: 'POST', body: { note: reason } })
    return { status: null }
  }
  const result = await apiRequest<{ status: AdminTripStatus }>(API_ENDPOINTS.trips.status(tripId), {
    method: 'POST',
    body: { operation: type, reason: reason || null, incidentId: incidentId ?? null },
  })
  return { status: result.status }
}
