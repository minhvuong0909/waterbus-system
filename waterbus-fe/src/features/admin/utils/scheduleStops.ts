import { t } from '../../../i18n'
import { parseWhole } from '../../../utils/inputRules'
import type { RouteStop, ScheduleDirection, ScheduleStopInput } from '../types'

/** Một bến của tuyến như form đang sửa; độ lệch là số phút Admin nhập. */
export interface ScheduleStopRow {
  routeStopId: string
  stationName: string
  included: boolean
  arrival: string
  departure: string
}

/** Những gì Admin đã đổi ở một bến, theo khoá bến của tuyến trong state của form. */
export type StopEdit = Pick<ScheduleStopRow, 'included' | 'arrival' | 'departure'>

const minutes = (value: number | null) => (value === null ? '' : String(value))

const isOffset = (value: string) => parseWhole(value, 0, 1440) !== null

/**
 * Các bến của tuyến theo thứ tự đi, sẵn sàng để sửa. Tuyến liệt kê bến theo chiều đi, nên chiều về đi ngược lại.
 * `saved` là các bến của lịch đang sửa: bến của tuyến mà lịch không phục vụ thì bắt đầu ở trạng thái bỏ chọn. Không
 * có `saved` thì mọi bến đều được chọn và để trống.
 */
export function buildStopRows(
  routeStops: RouteStop[],
  direction: ScheduleDirection,
  edits: Record<string, StopEdit>,
  saved?: ScheduleStopInput[],
): ScheduleStopRow[] {
  return [...routeStops]
    .sort((a, b) => (direction === 'Inbound' ? b.sequenceNo - a.sequenceNo : a.sequenceNo - b.sequenceNo))
    .map((stop) => {
      const kept = saved?.find((s) => s.routeStopId === stop.id)
      const initial: StopEdit = saved
        ? {
            included: kept !== undefined,
            arrival: minutes(kept?.arrivalOffsetMin ?? null),
            departure: minutes(kept?.departureOffsetMin ?? null),
          }
        : { included: true, arrival: '', departure: '' }
      return { routeStopId: stop.id, stationName: stop.stationName, ...initial, ...edits[stop.id] }
    })
}

/**
 * Các dòng ở dạng bến dừng backend cần (BR-STOP-01): chỉ bến được phục vụ, theo thứ tự. Bến đầu rời đúng giờ khởi
 * hành (lệch 0), bến cuối chỉ có giờ đến, các bến giữa có cả hai.
 */
export function toStopInputs(rows: ScheduleStopRow[]): ScheduleStopInput[] {
  const served = rows.filter((row) => row.included)
  return served.map((row, i) => ({
    routeStopId: row.routeStopId,
    arrivalOffsetMin: i === 0 ? null : Number(row.arrival),
    departureOffsetMin: i === 0 ? 0 : i === served.length - 1 ? null : Number(row.departure),
  }))
}

/** Lý do chưa gửi được bến dừng, hoặc undefined khi có ít nhất hai bến được phục vụ và tất cả đã có giờ. */
export function stopRowsError(rows: ScheduleStopRow[]): string | undefined {
  const served = rows.filter((row) => row.included)
  if (served.length < 2) return t('At least two stops must be served.')
  const filled = served.every(
    (row, i) =>
      (i === 0 || isOffset(row.arrival)) && (i === 0 || i === served.length - 1 || isOffset(row.departure)),
  )
  if (!filled) return t('Enter the minutes for every stop served.')
  // Giờ không lùi (backend từ chối): đến ≥ rời bến trước, rời ≥ đến, bến cuối đến sau giờ khởi hành.
  let previousLeave = 0
  for (const [i, row] of served.entries()) {
    const last = i === served.length - 1
    const arrival = i === 0 ? 0 : Number(row.arrival)
    const leave = last ? arrival : i === 0 ? 0 : Number(row.departure)
    if (arrival < previousLeave || leave < arrival || (last && arrival <= 0)) {
      return t('Times must not go backwards: each stop is reached after the previous one is left.')
    }
    previousLeave = leave
  }
  return undefined
}
