import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'

/** Same thresholds as the weather check: Caution = Medium alert, Danger = High alert. */
export type RouteWeatherStatus = 'Normal' | 'Caution' | 'Danger' | 'Unknown'

/** `GET /incidents/weather-now` item (BE `RouteWeatherDto`). Wind speeds are in m/s. */
export interface RouteWeather {
  routeId: string
  routeCode: string
  routeName: string
  status: RouteWeatherStatus
  windSpeedMs: number | null
  windGustMs: number | null
  conditionCode: number | null
  description: string | null
  observedAt: string | null
  pointsChecked: number
  pointsFailed: number
}

/** Weather now on each active route, worst pier first per route; never creates an alert. */
export function getCurrentWeather(): Promise<RouteWeather[]> {
  return apiRequest<RouteWeather[]>(API_ENDPOINTS.incidents.weatherNow)
}

/** m/s → km/h, one decimal, as the weather alerts show it. */
export const toKmh = (ms: number) => Math.round(ms * 3.6 * 10) / 10
