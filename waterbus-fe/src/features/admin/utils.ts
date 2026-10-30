import { numberLocale, t } from '../../i18n'
import type {
  AdminRoute,
  AdminStation,
  AdminTrip,
  AdminTripStatus,
  OperationType,
  WeatherAlert,
} from './types'

/** Day number the backend uses in `Schedule.DaysOfWeek`: 1 = Monday ... 7 = Sunday. */
export function backendDayOf(date: Date): number {
  return date.getDay() === 0 ? 7 : date.getDay()
}

/** Status a trip moves to for each operation (mock mode; the backend decides in `TripLifecycle`). */
export const OPERATION_RESULT: Record<OperationType, AdminTripStatus | null> = {
  OpenBoarding: 'Boarding',
  Start: 'EnRoute',
  Arrive: 'Arrived',
  Complete: 'Completed',
  Suspend: 'Suspended',
  Resume: 'Scheduled',
  Cancel: 'Cancelled',
  Terminate: 'Terminated',
  // Recording a decision or a report does not change the trip by itself.
  NoAction: null,
  SOS: null,
}

const ALLOWED: Record<AdminTripStatus, OperationType[]> = {
  Scheduled: ['OpenBoarding', 'Start', 'Suspend', 'Cancel'],
  Boarding: ['Start', 'Suspend', 'Cancel'],
  EnRoute: ['Arrive', 'Suspend', 'Terminate'],
  Suspended: ['Resume', 'Cancel'],
  Arrived: ['Complete'],
  Completed: [],
  Cancelled: [],
  Terminated: [],
}

/** Operations that make sense for a trip in this status (mock rules). */
export function allowedOperations(status: AdminTripStatus): OperationType[] {
  return ALLOWED[status]
}

/** Operations allowed for a trip: what the backend reports, or the mock rules. */
export function allowedOperationsOf(trip: Pick<AdminTrip, 'status' | 'allowedOperations'>): OperationType[] {
  return trip.allowedOperations ?? ALLOWED[trip.status]
}

/** Dispatcher operations (the captain's Start / Arrive / SOS have their own buttons). */
export const DISPATCH_OPERATIONS: OperationType[] = ['Suspend', 'Resume', 'Cancel', 'Terminate', 'Complete']

/** The trip is over for good: nothing more can be done with it. */
export const isFinished = (status: AdminTripStatus): boolean =>
  status === 'Completed' || status === 'Cancelled' || status === 'Terminated'

/** The trip still has to run or is running: the captain has something to do with it. */
export const isUpcomingOrRunning = (status: AdminTripStatus): boolean =>
  status === 'Scheduled' || status === 'Boarding' || status === 'EnRoute' || status === 'Suspended'

/** Trip names: from the backend's operations list when present, else by looking the id up. */
export const tripRouteName = (trip: AdminTrip, lookup: (id: string) => string): string =>
  trip.routeName ?? lookup(trip.routeId)

export const tripBoatCode = (trip: AdminTrip, lookup: (id: string) => string): string =>
  trip.boatCode ?? lookup(trip.boatId)

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
    timeZone: 'Asia/Ho_Chi_Minh',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** "07:30" */
export function formatClockTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(numberLocale(), { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit' })
}

/** Clock time with seconds, for live data that changes every few seconds. */
export function formatClockTimeWithSeconds(iso: string): string {
  return new Date(iso).toLocaleTimeString(numberLocale(), { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

/** Heading of an alert: mock alerts know the wind speed, real weather ones carry it in the message. */
export function weatherAlertTitle(alert: WeatherAlert): string {
  if (alert.kind && alert.kind !== 'Weather') return t('Captain emergency report')
  return alert.windSpeedKmh != null
    ? t('Level {level} • wind {speed} km/h', { level: alert.level, speed: alert.windSpeedKmh })
    : t('Level {level} weather warning', { level: alert.level })
}
