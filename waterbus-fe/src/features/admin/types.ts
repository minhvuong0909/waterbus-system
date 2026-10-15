import type { StationDto } from '../trips/types'

/** Station as the backend returns it (`GET /api/v1/stations`). */
export type AdminStation = StationDto

/** Route between two stations (BE `Route`). */
export interface AdminRoute {
  id: string
  code: string
  name: string
  departureStationId: string
  arrivalStationId: string
  estimatedDurationMinutes: number
  distanceKm: number
  isActive: boolean
}

/** Seats of a boat per cabin; a boat's `totalSeats` is always their sum. */
export interface SeatLayout {
  frontCabinSeats: number
  standardSeats: number
  outdoorSeats: number
}

/** Boat (BE `Boat`) with the seat layout it was created from. */
export interface AdminBoat extends SeatLayout {
  id: string
  code: string
  name: string
  registrationNumber: string
  totalSeats: number
  manufacturer: string
  yearBuilt: number
  isActive: boolean
}

/** BE `TripType`. */
export type AdminTripType = 'Commuter' | 'Sightseeing'

/** BE `TripStatus`. */
export type AdminTripStatus =
  | 'Scheduled'
  | 'Boarding'
  | 'InTransit'
  | 'Completed'
  | 'Cancelled'
  | 'Delayed'

/** Fixed weekly timetable entry (BE `Schedule`). Days follow the backend: 1 = Monday ... 7 = Sunday. */
export interface AdminSchedule {
  id: string
  routeId: string
  /** `HH:mm` */
  departureTime: string
  daysOfWeek: number[]
  tripType: AdminTripType
  isActive: boolean
}

/** A real run of a boat (BE `Trip`). */
export interface AdminTrip {
  id: string
  scheduleId: string | null
  routeId: string
  boatId: string
  /** ISO 8601 */
  departureTime: string
  arrivalTime: string
  tripType: AdminTripType
  status: AdminTripStatus
  basePrice: number
}

/** The backend has no pricing table: a trip's base price is set when it is generated. */
export interface FareSettings {
  basePrice: Record<AdminTripType, number>
  /** BE `Seat.PriceMultiplier` per cabin. */
  cabinMultiplier: { front: number; standard: number; outdoor: number }
}

export interface WeatherAlert {
  id: string
  /** 1 = warning, 2 = stop operations (wind above the 38 km/h threshold). */
  level: 1 | 2
  windSpeedKmh: number
  message: string
  /** ISO 8601 */
  issuedAt: string
}

/** What a dispatcher or captain did, kept as an audit trail. */
export type OperationType = 'Suspend' | 'Cancel' | 'Terminate' | 'Resume' | 'Start' | 'Arrive' | 'SOS'

export interface OperationLog {
  id: string
  type: OperationType
  tripId: string | null
  reason: string
  by: string
  /** ISO 8601 */
  at: string
}

/** Live position of a boat that is on the river. */
export interface FleetPosition {
  tripId: string
  boatId: string
  latitude: number
  longitude: number
  /** 0-1 along the route. */
  progress: number
  speedKmh: number
}

export type NewStation = Pick<
  AdminStation,
  'code' | 'name' | 'address' | 'latitude' | 'longitude' | 'orderIndex'
>

export type NewBoat = Pick<AdminBoat, 'code' | 'name' | 'registrationNumber' | 'manufacturer' | 'yearBuilt'> &
  SeatLayout

export interface NewSchedule {
  routeId: string
  departureTime: string
  daysOfWeek: number[]
  tripType: AdminTripType
}

/** Input of "generate trips": the schedule has no boat, so the admin picks one for the batch. */
export interface GenerateTripsInput {
  scheduleId: string
  boatId: string
  /** `YYYY-MM-DD` */
  fromDate: string
  toDate: string
}
