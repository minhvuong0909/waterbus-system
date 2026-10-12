export type TrackingMode = 'sightseeing' | 'transit'

export type PoiState = 'passed' | 'active' | 'next' | 'terminal'

export interface AudioEpisode {
  id: string
  title: string
  durationSec: number
  /** Seconds already played when the page first opens. */
  startAtSec: number
}

export interface TrackingPoi {
  id: string
  state: PoiState
  /** Position on the 920x660 map canvas. */
  x: number
  y: number
  /** Material Symbols name used on the map pin and itinerary card. */
  icon: string
  /** Map label, e.g. "Customs House". */
  mapTitle: string
  mapNote: string
  /** Itinerary card. */
  title: string
  tag: string
  description: string
  footer: string
  /** Story panel shown when this POI is active. */
  storyBadge: string
  storyTitle: string
  storyText: string
  storyMeta: string
  audio?: AudioEpisode
}

export interface TrackingTrip {
  code: string
  vesselType: string
  statusLabel: string
  seatLabel: string
  arrivalTime: string
  remainingLabel: string
  remainingDetail: string
}

export interface TrackingData {
  trip: TrackingTrip
  pois: TrackingPoi[]
  languages: string[]
  activePoiId: string
}
