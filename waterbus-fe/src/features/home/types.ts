export interface HeroCue {
  icon: string
  title: string
  subtitle: string
  iconClass: string
}

export interface RouteFilter {
  id: 'all' | 'line1' | 'line2' | 'sunset'
  label: string
}

export interface Story {
  id: string
  image: string
  badge: string
  badgeIcon: string
  badgeIconClass: string
  fromPriceVnd: number
  durationMins: number
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
