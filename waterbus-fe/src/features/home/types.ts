export interface RouteFilter {
  id: 'all' | 'transit' | 'sightseeing'
  label: string
}

export interface Step {
  number: string
  title: string
  description: string
  numberClass: string
}

export interface HomeData {
  routeFilters: RouteFilter[]
  steps: Step[]
}
