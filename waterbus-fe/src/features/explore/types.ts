/** A featured route shown on the Explore page (editorial content, not a bookable product). */
export interface Journey {
  id: string
  image: string
  /** Up to two curated labels, chosen per journey (all of them are shown). */
  tags: string[]
  title: string
  description: string
  route: string
}

export type PoiCategory = 'Skyline' | 'Heritage' | 'Culture' | 'Riverside'

export interface Landmark {
  id: string
  /** Missing while no photo of the right place is available: a neutral tile is shown instead. */
  image?: string
  category: PoiCategory
  title: string
  description: string
}

export interface ExploreData {
  journeys: Journey[]
  categories: PoiCategory[]
  landmarks: Landmark[]
}
