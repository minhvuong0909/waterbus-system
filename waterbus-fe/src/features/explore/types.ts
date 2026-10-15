export interface Journey {
  id: string
  image: string
  eyebrow: string
  tags: string[]
  title: string
  description: string
  route: string
}

export type PoiCategory = 'Skyline' | 'Heritage' | 'Culture' | 'Riverside'

export interface Landmark {
  id: string
  image: string
  category: PoiCategory
  routeTag: string
  title: string
  description: string
}

export interface MobilityMode {
  id: 'transit' | 'sightseeing'
  eyebrow: string
  title: string
  subtitle: string
  icon: string
  chip: string
  features: string[]
  cta: string
}

export interface ExploreEpisode {
  id: string
  eyebrow: string
  title: string
  subtitle: string
  durationSec: number
  startAtSec: number
  thumbnail: string
}

export interface CorridorPin {
  id: string
  icon: string
  title: string
  subtitle?: string
  /** Position in percent of the map panel. */
  x: number
  y: number
  emphasis?: 'hub' | 'stop' | 'poi'
}

export interface ExploreData {
  journeys: Journey[]
  modes: MobilityMode[]
  categories: PoiCategory[]
  landmarks: Landmark[]
  languages: string[]
  episode: ExploreEpisode
  pins: CorridorPin[]
}
