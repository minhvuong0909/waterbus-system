export interface HeroCue {
  icon: string
  title: string
  subtitle: string
  iconClass: string
}

export interface RouteFilter {
  id: 'all' | 'transit' | 'sightseeing'
  label: string
}

export interface Story {
  id: string
  image: string
  badge: string
  badgeIcon: string
  badgeIconClass: string
  /** Lowest active Sightseeing fare from `GET /fares`; absent when there is none. */
  fromPriceVnd?: number
  /** Only known in mock mode: the backend has no tour duration. */
  durationMins?: number
  location: string
  title: string
  description: string
  trailingIcon: string
  trailingIconClass: string
}

export interface Step {
  number: string
  title: string
  description: string
  numberClass: string
}

export interface HomeData {
  heroCues: HeroCue[]
  routeFilters: RouteFilter[]
  stories: Story[]
  steps: Step[]
}
