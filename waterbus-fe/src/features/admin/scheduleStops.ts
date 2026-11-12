import { t } from '../../i18n'
import type { RouteStop, ScheduleDirection, ScheduleStopInput } from './types'

/** One stop of the route as a form edits it; the offsets are the minutes the admin typed. */
export interface ScheduleStopRow {
  routeStopId: string
  stationName: string
  included: boolean
  arrival: string
  departure: string
}

/** What the admin changed on one stop, keyed by route stop in the form state. */
export type StopEdit = Pick<ScheduleStopRow, 'included' | 'arrival' | 'departure'>

const minutes = (value: number | null) => (value === null ? '' : String(value))

const isOffset = (value: string) => {
  const n = Number(value)
  return value.trim() !== '' && Number.isInteger(n) && n >= 0 && n <= 1440
}

/**
 * The stops of a route in travel order, ready to edit. The route lists its stops outbound, so an
 * inbound run goes through them backwards. `saved` are the stops of the schedule being edited:
 * route stops it does not serve start unticked. Without `saved` every stop starts ticked and empty.
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
 * The rows as the stops the backend expects (BR-STOP-01): only the served stops, in order. The
 * first leaves at the departure time (offset 0), the last only arrives, the others do both.
 */
export function toStopInputs(rows: ScheduleStopRow[]): ScheduleStopInput[] {
  const served = rows.filter((row) => row.included)
  return served.map((row, i) => ({
    routeStopId: row.routeStopId,
    arrivalOffsetMin: i === 0 ? null : Number(row.arrival),
    departureOffsetMin: i === 0 ? 0 : i === served.length - 1 ? null : Number(row.departure),
  }))
}

/** Why the stops cannot be sent yet, or undefined when at least two are served and all are timed. */
export function stopRowsError(rows: ScheduleStopRow[]): string | undefined {
  const served = rows.filter((row) => row.included)
  if (served.length < 2) return t('At least two stops must be served.')
  const filled = served.every(
    (row, i) =>
      (i === 0 || isOffset(row.arrival)) && (i === 0 || i === served.length - 1 || isOffset(row.departure)),
  )
  return filled ? undefined : t('Enter the minutes for every stop served.')
}
