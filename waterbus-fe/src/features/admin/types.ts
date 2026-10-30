import type { StationDto, TripStatus, TripType } from '../trips/types'

/** Station as the backend returns it (`GET /api/v1/stations`). */
export type AdminStation = StationDto

/** Boat (BE `BoatDto`). */
export interface AdminBoat {
  id: string
  code: string
  name: string
  registrationNumber: string
  totalSeats: number
  manufacturer: string
  yearBuilt: number
  isActive: boolean
  captainUserId: string | null
}

/** A trip as the operations list returns it (BE `TripOperationItemDto`). */
export interface AdminTrip {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  /** '' while no boat is assigned. */
  boatId: string
  boatCode: string | null
  /** ISO 8601 */
  departureTime: string
  arrivalTime: string
  tripType: TripType
  status: TripStatus
  /** What the backend allows right now for this trip (BE `TripLifecycle`). */
  allowedOperations: OperationType[]
}

/** An open incident shown as a warning: weather alerts from the weather guard, or a captain's SOS. */
export interface WeatherAlert {
  id: string
  /** 1 = warning, 2 = stop operations (BE severity Medium = 1, High = 2). */
  level: 1 | 2
  message: string
  /** ISO 8601 */
  issuedAt: string
  /** BE `IncidentType`. */
  kind?: 'Weather' | 'Safety' | 'Technical' | 'Other'
  /** Trips linked to the alert (BE `IncidentTrip`), when known. */
  affectedTripCount?: number
  /** Linked trips the admin has not decided about yet (BE `PendingDecisionCount`). */
  pendingDecisionCount?: number
  /** Route the alert is about, e.g. "RT01" (weather alerts are per route). */
  routeCode?: string | null
  /** Who raised it: `WeatherService` for the weather guard, otherwise a person (captain SOS). */
  source?: string
  /** The linked trips and what the admin decided for each (null = not decided yet). Only on request. */
  trips?: AlertTrip[]
}

/** A trip an alert affects (BE `AffectedTripDto`). */
export interface AlertTrip {
  tripId: string
  decision: string | null
  departureTime?: string
  tripStatus?: TripStatus
  decisionAt?: string | null
}

/** What an admin or a captain did to a trip, kept as an audit trail (BE `TripOperationEvent`). */
export type OperationType =
  | 'OpenBoarding'
  | 'Start'
  | 'Arrive'
  | 'Complete'
  | 'Suspend'
  | 'Resume'
  | 'Cancel'
  | 'Terminate'
  | 'NoAction'
  | 'SOS'

export interface OperationLog {
  id: string
  type: OperationType
  tripId: string | null
  reason: string
  by: string
  /** ISO 8601 */
  at: string
}

export type NewStation = Pick<
  AdminStation,
  'code' | 'name' | 'address' | 'latitude' | 'longitude' | 'orderIndex'
>

export type NewBoat = Pick<AdminBoat, 'code' | 'name' | 'registrationNumber' | 'manufacturer' | 'yearBuilt' | 'totalSeats'>

/** Last known position of a boat, from `GET /tracking/boats` or the `boatPosition` hub event. */
export interface LiveBoatPosition {
  boatId: string
  /** Set while the boat runs a trip (Boarding, EnRoute or Suspended). */
  tripId: string | null
  latitude: number
  longitude: number
  speedKnots: number | null
  /** ISO 8601 */
  observedAt: string
}

/** Result of `POST /incidents/weather-check`. */
export interface WeatherCheckResult {
  routesChecked: number
  incidentsCreated: number
  incidentsUpdated: number
  /** Alerts the guard closed because the weather is calm again. */
  incidentsResolved?: number
  observationFailures: number
}
