import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { RouteDetail, RouteInput, RouteListItem, RouteStopInput } from '../types'
import { fetchAllPages, setActive } from './catalogRequests'

/** `GET /admin/routes`: every route; `activeOnly` for the routes a new schedule may use. */
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
 * Saves a route: `PUT /admin/routes/{id}` for its details, then `PUT …/stops` when the stops
 * changed. A route already used by a schedule or trip keeps its stops (the backend answers 409),
 * so they are only sent while the structure is not locked.
 */
export async function updateRoute(current: RouteDetail, input: RouteInput): Promise<void> {
  const { name, serviceType, estimatedDurationMinutes, distanceKm, stops } = input
  await apiRequest<void>(API_ENDPOINTS.admin.route(current.id), {
    method: 'PUT',
    body: { name, serviceType, estimatedDurationMinutes, distanceKm },
  })
  if (!current.structureLocked && !sameStops(stops, current.stops)) {
    await apiRequest(API_ENDPOINTS.admin.routeStops(current.id), { method: 'PUT', body: { stops } })
  }
}

/** A route with an active schedule or an unfinished trip cannot be switched off (409). */
export const setRouteActive = (id: string, active: boolean) =>
  setActive(API_ENDPOINTS.admin.route(id), active)
