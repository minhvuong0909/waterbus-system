export type DepartureStatusKind = 'ontime' | 'boarding' | 'scheduled' | 'twilight'

export type DepartureKind = 'transit' | 'sightseeing'

export interface Departure {
  id: string
  /** Id of the matching Trip, so "Select" opens the right trip detail. */
  tripId: string
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
  /** Line the departure runs on; used by the corridor filter. */
  line: 'line1' | 'line2' | 'sunset' | 'direct'
  priceVnd: number
}

export interface Pier {
  id: string
  name: string
  subtitle: string
  /** Position on the corridor map, in percent of the map canvas. */
  x: number
  y: number
  kind: 'hub' | 'stop' | 'terminus'
}

export interface Vessel {
  code: string
  status: string
  speedKmh: number
  nextPier: string
  /** Position on the corridor map, in percent of the map canvas. */
  x: number
  y: number
}

export interface CorridorMap {
  piers: Pier[]
  vessel: Vessel
}

// ---- Trip search -----------------------------------------------------------

export type TripType = 'one-way' | 'round-trip'

export interface PierOption {
  id: string
  /** Full label used in forms, e.g. "Bach Dang Pier (D1)". */
  name: string
  /** Pier name without the district code, e.g. "Bach Dang Pier". */
  pierName: string
  /** District the pier is in, e.g. "District 1". */
  district: string
  /** Short label used on route cards, e.g. "Bach Dang". */
  shortName: string
  subtitle: string
}

export interface SearchQuery {
  tripType: TripType
  originId: string
  destinationId: string
  /** Id of the selected DateOption. */
  dateId: string
  passengers: string
  passengerNote: string
}

export interface DateOption {
  id: string
  weekday: string
  label: string
  /** Full date with year, e.g. "Dec 16, 2025". */
  fullLabel: string
  fromFareVnd: number
  isToday?: boolean
  tripCount?: number
}

export interface NetworkRoute {
  id: string
  originId: string
  destinationId: string
  badge: string
  summary: string
  fromFareVnd: number
}

export interface AmenityChip {
  icon: string
  label: string
}

export type TripVariant = 'featured' | 'default' | 'sunset'

export type TimeBand = 'morning' | 'afternoon' | 'sunset'

export interface Trip {
  id: string
  vesselCode: string
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
  /** Full date with year, e.g. "Dec 16, 2025". */
  dateFull: string
  /** Date without the year, e.g. "Dec 16". */
  dateShort: string
  timeline: TimelineStep[]
  amenityDetails: AmenityDetail[]
  boardingSteps: BoardingStep[]
}
