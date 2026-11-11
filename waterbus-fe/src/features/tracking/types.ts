import type { TripStatus } from '../trips/types'

/** A stop of the tracked trip, placed at its station's coordinates. */
export interface TrackedStop {
  callId: string
  stationName: string
  visitOrder: number
  latitude: number
  longitude: number
  /** `TripStopCall` status from the backend (e.g. "Scheduled", "Departed"). */
  status: string
  estimatedArrivalTime: string | null
  estimatedDepartureTime: string | null
}

/** The trip a traveler follows: `GET /trips/{id}` with the stops on the map. */
export interface TrackedTrip {
  tripId: string
  routeName: string
  status: TripStatus
  boat: { boatId: string; code: string; name: string } | null
  departureTime: string
  arrivalTime: string
  stops: TrackedStop[]
}

/** Last position the boat reported through the boat-tracking hub. */
export interface BoatFix {
  latitude: number
  longitude: number
  speedKnots: number | null
  observedAt: string
}
