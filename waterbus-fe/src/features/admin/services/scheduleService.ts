import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { ScheduleDetail, ScheduleInput, ScheduleListItem, ScheduleStopInput } from '../types'
import { fetchAllPages, setActive } from './catalogRequests'

/** `GET /admin/schedules`: every schedule, switched-off ones included. */
export const getSchedules = () => fetchAllPages<ScheduleListItem>(API_ENDPOINTS.admin.schedules)

export const getSchedule = (id: string) =>
  apiRequest<ScheduleDetail>(API_ENDPOINTS.admin.schedule(id))

export const createSchedule = (input: ScheduleInput) =>
  apiRequest<string>(API_ENDPOINTS.admin.schedules, { method: 'POST', body: input })

const sameStops = (a: ScheduleStopInput[], b: ScheduleStopInput[]) =>
  a.length === b.length &&
  a.every(
    (stop, i) =>
      stop.routeStopId === b[i].routeStopId &&
      stop.arrivalOffsetMin === b[i].arrivalOffsetMin &&
      stop.departureOffsetMin === b[i].departureOffsetMin,
  )

/**
 * Saves a schedule: `PUT /admin/schedules/{id}` for its times, then `PUT …/stops` when the stops
 * changed. The route never changes, and a schedule that already produced trips keeps its stops
 * (BR-SCH-06), so they are only sent while the structure is not locked.
 */
export async function updateSchedule(current: ScheduleDetail, input: ScheduleInput): Promise<void> {
  const { tripType, direction, departureTime, daysOfWeek, effectiveFrom, effectiveTo, stops } = input
  await apiRequest<void>(API_ENDPOINTS.admin.schedule(current.id), {
    method: 'PUT',
    body: { tripType, direction, departureTime, daysOfWeek, effectiveFrom, effectiveTo },
  })
  if (!current.structureLocked && !sameStops(stops, current.stops)) {
    await apiRequest(API_ENDPOINTS.admin.scheduleStops(current.id), { method: 'PUT', body: { stops } })
  }
}

export const setScheduleActive = (id: string, active: boolean) =>
  setActive(API_ENDPOINTS.admin.schedule(id), active)
