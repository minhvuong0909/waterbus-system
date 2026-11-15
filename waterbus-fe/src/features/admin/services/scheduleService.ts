import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { ScheduleDetail, ScheduleInput, ScheduleListItem, ScheduleStopInput } from '../types'
import { fetchAllPages } from '../../../services/paging'
import { setActive } from './catalogRequests'

/** `GET /admin/schedules`: mọi lịch chạy, kể cả lịch đã tắt. */
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
 * Lưu một lịch chạy: `PUT /admin/schedules/{id}` cho giờ chạy, rồi `PUT …/stops` khi bến dừng thay đổi. Tuyến không
 * bao giờ đổi, và lịch đã sinh chuyến giữ nguyên bến dừng (BR-SCH-06), nên chỉ gửi bến khi cấu trúc chưa bị khoá.
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
