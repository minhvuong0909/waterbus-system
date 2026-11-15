import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { t } from '../../../i18n'
import type { RouteDetail, RouteInput, RouteListItem, RouteStopInput } from '../types'
import { fetchAllPages } from '../../../services/paging'
import { setActive } from './catalogRequests'

/** `GET /admin/routes`: mọi tuyến; `activeOnly` để lấy các tuyến lịch chạy mới được dùng. */
export const getRoutes = (activeOnly = false) =>
  fetchAllPages<RouteListItem>(API_ENDPOINTS.admin.routes, { isActive: activeOnly ? true : undefined })

export const getRoute = (id: string) => apiRequest<RouteDetail>(API_ENDPOINTS.admin.route(id))

export const createRoute = (input: RouteInput) =>
  apiRequest<string>(API_ENDPOINTS.admin.routes, { method: 'POST', body: input })

const sameStops = (a: RouteStopInput[], b: RouteStopInput[]) =>
  a.length === b.length &&
  a.every(
    (stop, i) =>
      stop.stationId === b[i].stationId &&
      stop.canBoard === b[i].canBoard &&
      stop.canAlight === b[i].canAlight,
  )

/**
 * Lưu một tuyến: `PUT /admin/routes/{id}` cho thông tin, rồi `PUT …/stops` khi bến dừng thay đổi. Tuyến đã được
 * lịch chạy hoặc chuyến dùng giữ nguyên bến dừng (backend trả 409), nên chỉ gửi bến khi cấu trúc chưa bị khoá.
 */
export async function updateRoute(current: RouteDetail, input: RouteInput): Promise<void> {
  const { name, serviceType, estimatedDurationMinutes, distanceKm, stops } = input
  await apiRequest<void>(API_ENDPOINTS.admin.route(current.id), {
    method: 'PUT',
    body: { name, serviceType, estimatedDurationMinutes, distanceKm },
  })
  if (!current.structureLocked && !sameStops(stops, current.stops)) {
    try {
      await apiRequest(API_ENDPOINTS.admin.routeStops(current.id), { method: 'PUT', body: { stops } })
    } catch (error) {
      // Thông tin tuyến đã lưu ở lệnh trước; nói rõ để Admin không tưởng cả hai phần đều chưa lưu.
      if (error instanceof ApiError) {
        throw new ApiError(
          error.status,
          t('The route details were saved, but the stops were not changed: {reason}', { reason: error.message }),
          error.fieldErrors,
          error.traceId,
        )
      }
      throw error
    }
  }
}

/** Tuyến còn lịch chạy hoạt động hoặc chuyến chưa xong thì không tắt được (409). */
export const setRouteActive = (id: string, active: boolean) =>
  setActive(API_ENDPOINTS.admin.route(id), active)
