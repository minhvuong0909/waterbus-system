export type DepartureStatusKind = 'ontime' | 'boarding' | 'scheduled' | 'twilight'

export type DepartureKind = 'transit' | 'sightseeing'

export interface Departure {
  id: string
  /** The matching trip, so "Select" opens the right trip detail. */
  trip: Trip
  time: string
  /** Display text, already in the active language. */
  status: string
  /** Language-independent status, used for styling. */
  statusKind: DepartureStatusKind
  kind: DepartureKind
  /** Origin pier, or the tour name for sightseeing trips. */
  from: string
  to?: string
  vesselCode?: string
  /** Detail chips shown under the route, e.g. "12 min voyage". */
  details: string[]
  priceVnd: number
}

export interface Pier {
  id: string
  name: string
  /** District, shown under the name. */
  subtitle: string
  /** Station code, e.g. "ST01"; names the photo file of the pier. */
  code: string
  address: string
  latitude: number
  longitude: number
}

export interface Vessel {
  code: string
  status: string
  speedKmh: number
  nextPier: string
  latitude: number
  longitude: number
}

export interface CorridorMap {
  piers: Pier[]
  vessel: Vessel | null
}

// ---- Trip search -----------------------------------------------------------

export type JourneyType = 'one-way' | 'round-trip'

/** Regular river transit, or a sightseeing tour (BE `TripType`: Commuter / Sightseeing). */
export type TripKind = 'transit' | 'sightseeing'

export interface PierOption {
  id: string
  /** Station code from the backend, e.g. "ST01". */
  code: string
  /** Full label used in forms, e.g. "Bach Dang Pier (D1)". */
  name: string
  /** Pier name without the district code, e.g. "Bach Dang Pier". */
  pierName: string
  /** District the pier is in, e.g. "District 1". */
  district: string
  latitude: number
  longitude: number
  /** Short label used on route cards, e.g. "Bach Dang". */
  shortName: string
  subtitle: string
}

export interface SearchQuery {
  tripType: JourneyType
  originId: string
  destinationId: string
  /** Selected date as `YYYY-MM-DD` (the id of a DateOption). */
  dateId: string
  /** Return date for a round trip (BR-SRCH-01), never before `dateId`. */
  returnDateId?: string
}

export interface DateOption {
  /** Calendar date as `YYYY-MM-DD`. */
  id: string
  weekday: string
  label: string
  /** Full date with year, e.g. "Dec 16, 2025". */
  fullLabel: string
  /** Lowest fare of the day. Not provided by the backend yet, so usually absent. */
  fromFareVnd?: number
  isToday?: boolean
  tripCount?: number
}

export interface NetworkRoute {
  id: string
  originId: string
  destinationId: string
  badge: string
  summary: string
  /** Lowest active fare of the route's trip type, from `GET /fares`. */
  fromFareVnd?: number
}

export interface AmenityChip {
  icon: string
  label: string
}

export type TripVariant = 'featured' | 'default' | 'sunset'

export type TimeBand = 'morning' | 'afternoon' | 'sunset'

export interface Trip {
  id: string
  kind: TripKind
  /**
   * Vessel code. The backend's TripDto has no boat info yet, so for real trips this holds a
   * short trip reference instead (see `tripReference`).
   */
  vesselCode: string
  /** Vessel name; for real trips the route name until TripDto exposes the boat. */
  vesselName: string
  /** Pill shown at the top of the card, e.g. "Express Line 1". */
  lineLabel: string
  /** Optional highlighted pill, e.g. "RECOMMENDED • FASTEST". */
  highlight?: { icon: string; label: string }
  tagline?: string
  variant: TripVariant
  departTime: string
  arriveTime: string
  originGate: string
  destinationGate: string
  durationMins: number
  /** "Direct", "Leisure Speed", "Landmark Loop"... */
  crossingKind: string
  /** True for a non-stop direct crossing. */
  direct: boolean
  crossingCaption: string
  seatsCount: number
  /** True for "Limited" / low availability styling. */
  seatsLow?: boolean
  band: TimeBand
  amenities: AmenityChip[]
  fareVnd: number
  fareLabel: string
}

export interface TimelineStep {
  time: string
  title: string
  description: string
  tone: 'teal' | 'amber' | 'dark'
}

export interface AmenityDetail {
  icon: string
  title: string
  description: string
}

export interface BoardingStep {
  title: string
  description: string
}

export interface TripDetail extends Trip {
  /** Full pier labels, e.g. "Bach Dang Pier (D1)". */
  originPierLabel: string
  destinationPierLabel: string
  originPierName: string
  destinationPierName: string
  /** Short names without "Pier", e.g. "Bach Dang". */
  originShortName: string
  destinationShortName: string
  originDistrict: string
  destinationDistrict: string
  dateLabel: string
  /** The day of the trip as `YYYY-MM-DD`. */
  dateId: string
  /** Full date with year, e.g. "Dec 16, 2025". */
  dateFull: string
  /** Date without the year, e.g. "Dec 16". */
  dateShort: string
  timeline: TimelineStep[]
  amenityDetails: AmenityDetail[]
  boardingSteps: BoardingStep[]
}

// ---- Backend DTOs -----------------------------------------------------------

/** `GET /api/v1/stations` item (StationDto). */
export interface StationDto {
  id: string
  code: string
  name: string
  address: string
  latitude: number
  longitude: number
  orderIndex: number
  isActive: boolean
}

/** `GET /api/v1/trips/search` item (TripDto). `tripType` is "Commuter" or "Sightseeing". */
export interface TripDto {
  id: string
  routeName: string
  departureStationName: string
  arrivalStationName: string
  departureTime: string
  arrivalTime: string
  durationMinutes: number
  tripType: string
  basePrice: number
  availableSeatsCount: number
}

/** BE `TripType`. */
export type TripType = 'Commuter' | 'Sightseeing'

/** BE `TripStatus` (BR-OPS-01). */
export type TripStatus =
  | 'Scheduled'
  | 'Boarding'
  | 'EnRoute'
  | 'Suspended'
  | 'Arrived'
  | 'Completed'
  | 'Cancelled'
  | 'Terminated'
