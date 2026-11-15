import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'

/** Cùng ngưỡng với lượt kiểm tra thời tiết: Caution = cảnh báo Medium, Danger = cảnh báo High. */
export type RouteWeatherStatus = 'Normal' | 'Caution' | 'Danger' | 'Unknown'

/** Một mục của `GET /incidents/weather-now` (BE `RouteWeatherDto`). Tốc độ gió tính bằng m/s. */
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

/** Thời tiết hiện tại trên từng tuyến đang hoạt động, bến xấu nhất của mỗi tuyến; không bao giờ tạo cảnh báo. */
export function getCurrentWeather(): Promise<RouteWeather[]> {
  return apiRequest<RouteWeather[]>(API_ENDPOINTS.incidents.weatherNow)
}

/** m/s → km/h, một chữ số thập phân, giống cách cảnh báo thời tiết hiển thị. */
export const toKmh = (ms: number) => Math.round(ms * 3.6 * 10) / 10
