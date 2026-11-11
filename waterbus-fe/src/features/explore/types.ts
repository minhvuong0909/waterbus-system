import type { Departure, SearchQuery } from '../trips/types'

export type PoiCategory = 'Heritage' | 'Skyline' | 'Riverside'

/** A point of interest narrated on sightseeing trips (BR-EXP-01). */
export interface Landmark {
  id: string
  /** Missing while no photo of the right place is available: a neutral tile is shown instead. */
  image?: string
  category: PoiCategory
  title: string
  description: string
}

export interface ExploreData {
  categories: PoiCategory[]
  landmarks: Landmark[]
}

/** A real upcoming sightseeing trip, with the search that opens its detail page. */
export interface SightseeingDeparture {
  departure: Departure
  search: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>
}
