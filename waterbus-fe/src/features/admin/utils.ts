import { numberLocale } from '../../i18n'
import type {
  AdminRoute,
  AdminStation,
  AdminTrip,
  AdminTripStatus,
  OperationType,
} from './types'

/** Day number the backend uses in `Schedule.DaysOfWeek`: 1 = Monday ... 7 = Sunday. */
export function backendDayOf(date: Date): number {
  return date.getDay() === 0 ? 7 : date.getDay()
}

export const WEEKDAYS = [
  { day: 1, label: 'Mon' },
  { day: 2, label: 'Tue' },
  { day: 3, label: 'Wed' },
  { day: 4, label: 'Thu' },
  { day: 5, label: 'Fri' },
  { day: 6, label: 'Sat' },
  { day: 7, label: 'Sun' },
] as const

/** Status a trip moves to for each operation. */
export const OPERATION_RESULT: Record<OperationType, AdminTripStatus | null> = {
  Suspend: 'Delayed',
  Resume: 'Scheduled',
  Cancel: 'Cancelled',
  // The backend has no "terminated" status, so ending a running trip early is recorded as cancelled.
  Terminate: 'Cancelled',
  Start: 'InTransit',
  Arrive: 'Completed',
  // An SOS is a report, it does not change the trip by itself.
  SOS: null,
}

const ALLOWED: Record<AdminTripStatus, OperationType[]> = {
  Scheduled: ['Suspend', 'Cancel', 'Start'],
  Boarding: ['Suspend', 'Cancel', 'Start'],
  InTransit: ['Suspend', 'Terminate', 'Arrive'],
  Delayed: ['Resume', 'Cancel'],
  Completed: [],
  Cancelled: [],
}

/** Operations that make sense for a trip in this status. */
export function allowedOperations(status: AdminTripStatus): OperationType[] {
  return ALLOWED[status]
}

/** Dispatcher operations (the captain's Start / Arrive / SOS have their own buttons). */
export const DISPATCH_OPERATIONS: OperationType[] = ['Suspend', 'Resume', 'Cancel', 'Terminate']

export const isFinished = (status: AdminTripStatus): boolean =>
  status === 'Completed' || status === 'Cancelled'

/** Stations of a route in river order, from its departure station to its arrival station. */
export function routeStations(route: AdminRoute, stations: AdminStation[]): AdminStation[] {
  const from = stations.find((s) => s.id === route.departureStationId)
  const to = stations.find((s) => s.id === route.arrivalStationId)
  if (!from || !to) return []
  const [low, high] = [Math.min(from.orderIndex, to.orderIndex), Math.max(from.orderIndex, to.orderIndex)]
  const between = stations
    .filter((s) => s.orderIndex >= low && s.orderIndex <= high)
    .sort((a, b) => a.orderIndex - b.orderIndex)
  return from.orderIndex <= to.orderIndex ? between : between.reverse()
}

type Point = [latitude: number, longitude: number]

/** Point at `progress` (0-1) of the way along a polyline, by distance in degrees. */
export function positionAlong(points: Point[], progress: number): Point {
  if (points.length === 0) return [0, 0]
  if (points.length === 1) return points[0]
  const lengths = points.slice(1).map((p, i) => Math.hypot(p[0] - points[i][0], p[1] - points[i][1]))
  const total = lengths.reduce((sum, l) => sum + l, 0)
  let target = Math.min(Math.max(progress, 0), 1) * total
  for (let i = 0; i < lengths.length; i++) {
    if (target <= lengths[i] || i === lengths.length - 1) {
      const ratio = lengths[i] === 0 ? 0 : Math.min(target / lengths[i], 1)
      return [
        points[i][0] + (points[i + 1][0] - points[i][0]) * ratio,
        points[i][1] + (points[i + 1][1] - points[i][1]) * ratio,
      ]
    }
    target -= lengths[i]
  }
  return points[points.length - 1]
}

/** Share of the trip already sailed (0-1) at `now`, from its departure and arrival times. */
export function tripProgress(trip: Pick<AdminTrip, 'departureTime' | 'arrivalTime'>, now: number): number {
  const start = new Date(trip.departureTime).getTime()
  const end = new Date(trip.arrivalTime).getTime()
  return end <= start ? 0 : Math.min(Math.max((now - start) / (end - start), 0), 1)
}

/** Seats of a boat across the three cabins. */
export const totalSeatsOf = (layout: {
  frontCabinSeats: number
  standardSeats: number
  outdoorSeats: number
}): number => layout.frontCabinSeats + layout.standardSeats + layout.outdoorSeats

/** Two trips of one boat must not overlap in time. */
export function overlaps(
  a: Pick<AdminTrip, 'departureTime' | 'arrivalTime'>,
  b: Pick<AdminTrip, 'departureTime' | 'arrivalTime'>,
): boolean {
  return (
    new Date(a.departureTime).getTime() < new Date(b.arrivalTime).getTime() &&
    new Date(b.departureTime).getTime() < new Date(a.arrivalTime).getTime()
  )
}

/** "Oct 12, 07:30" in the active language. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString(numberLocale(), {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** "07:30" */
export function formatClockTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(numberLocale(), { hour: '2-digit', minute: '2-digit' })
}
