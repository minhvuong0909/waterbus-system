import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { withDelay } from '../../../mocks/delay'
import { STATION_SEEDS } from '../../../mocks/tripSearch'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { t } from '../../../i18n'
import { toIsoDate } from '../../../utils/time'
import type { StationDto } from '../../trips/types'
import type {
  AdminBoat,
  AdminRoute,
  AdminSchedule,
  AdminStation,
  AdminTrip,
  AdminTripStatus,
  AdminTripType,
  FareSettings,
  FleetPosition,
  GenerateTripsInput,
  NewBoat,
  NewSchedule,
  NewStation,
  OperationLog,
  OperationType,
  WeatherAlert,
} from '../types'
import {
  OPERATION_RESULT,
  allowedOperations,
  backendDayOf,
  overlaps,
  positionAlong,
  routeStations,
  totalSeatsOf,
  tripProgress,
} from '../utils'

// The backend has no admin endpoints yet (roadmap P1-BE-01..04, P3, P4). Everything below works on
// an in-memory copy of the data, so changes last until the page is reloaded. Only the station list
// comes from the backend (`GET /stations`), and the copy starts from it. Each function follows the
// contract of the planned endpoint, so it can later be swapped for an `apiRequest` call.

const MAX_GENERATE_DAYS = 60
const MINUTE = 60_000

interface AdminDb {
  stations: AdminStation[]
  routes: AdminRoute[]
  boats: AdminBoat[]
  schedules: AdminSchedule[]
  trips: AdminTrip[]
  fares: FareSettings
  alerts: WeatherAlert[]
  logs: OperationLog[]
}

const newId = () => crypto.randomUUID()

function conflict(message: string): ApiError {
  return new ApiError(409, message)
}

function notFound(): ApiError {
  return new ApiError(404, t('Not found.'))
}

async function loadStations(): Promise<AdminStation[]> {
  return USE_MOCK ? STATION_SEEDS.map((s) => ({ ...s })) : apiRequest<StationDto[]>(API_ENDPOINTS.stations)
}

/** Trips around "now": the finished ones, the one on the river, and the ones still to come. */
function seedTrips(routeId: string, boatId: string, durationMinutes: number, now: number): AdminTrip[] {
  const status = (departure: number): AdminTripStatus => {
    const arrival = departure + durationMinutes * MINUTE
    if (arrival <= now) return 'Completed'
    if (departure <= now) return 'InTransit'
    return departure - now <= 15 * MINUTE ? 'Boarding' : 'Scheduled'
  }
  // One trip departs 20 minutes ago, so it is on the river now; the others are an hour apart.
  const first = now - 20 * MINUTE
  return [-2, -1, 0, 1, 2].map((step) => {
    const departure = first + step * 60 * MINUTE
    return {
      id: newId(),
      scheduleId: null,
      routeId,
      boatId,
      departureTime: new Date(departure).toISOString(),
      arrivalTime: new Date(departure + durationMinutes * MINUTE).toISOString(),
      tripType: 'Commuter' as AdminTripType,
      status: status(departure),
      basePrice: 15000,
    }
  })
}

async function createDb(): Promise<AdminDb> {
  const stations = await loadStations()
  const byCode = (code: string) => stations.find((s) => s.code === code)
  const first = byCode('ST01') ?? stations[0]
  const last = byCode('ST05') ?? stations[stations.length - 1]

  // Seeded by the backend: route RT01 and boat SWB-01 with 60 seats (ApplicationDbContextInitializer).
  const route: AdminRoute = {
    id: newId(),
    code: 'RT01',
    name: 'Bạch Đằng - Linh Đông',
    departureStationId: first?.id ?? '',
    arrivalStationId: last?.id ?? '',
    estimatedDurationMinutes: 45,
    distanceKm: 10.8,
    isActive: true,
  }
  const boat: AdminBoat = {
    id: newId(),
    code: 'SWB-01',
    name: 'Saigon Waterbus 01',
    registrationNumber: 'SG-8899-WS',
    frontCabinSeats: 12,
    standardSeats: 36,
    outdoorSeats: 12,
    totalSeats: 60,
    manufacturer: 'Saigon Shipyard',
    yearBuilt: 2024,
    isActive: true,
  }
  const now = Date.now()
  const schedule = (departureTime: string, tripType: AdminTripType): AdminSchedule => ({
    id: newId(),
    routeId: route.id,
    departureTime,
    daysOfWeek: [1, 2, 3, 4, 5, 6, 7],
    tripType,
    isActive: true,
  })

  return {
    stations,
    routes: [route],
    boats: [boat],
    schedules: [schedule('07:00', 'Commuter'), schedule('12:00', 'Commuter'), schedule('17:30', 'Sightseeing')],
    trips: seedTrips(route.id, boat.id, route.estimatedDurationMinutes, now),
    // The backend has no pricing table. 15,000 is the default Trip.BasePrice; the sightseeing price
    // and the cabin multipliers follow the demo data (multipliers as seeded on Seat.PriceMultiplier).
    fares: {
      basePrice: { Commuter: 15000, Sightseeing: 45000 },
      cabinMultiplier: { front: 1.2, standard: 1.0, outdoor: 1.1 },
    },
    alerts: [
      {
        id: newId(),
        level: 1,
        windSpeedKmh: 41,
        message: 'Wind above the 38 km/h threshold on the Saigon River corridor.',
        issuedAt: new Date(now - 10 * MINUTE).toISOString(),
      },
    ],
    logs: [],
  }
}

let dbPromise: Promise<AdminDb> | undefined
const db = () => (dbPromise ??= createDb())

/** Resolves with `read(db)` after a short delay, as a copy the caller may not use to change the store. */
async function query<T>(read: (data: AdminDb) => T): Promise<T> {
  const data = await db()
  return withDelay(read(data))
}

// ---- Stations -----------------------------------------------------------------

export const getStations = () => query((d) => [...d.stations].sort((a, b) => a.orderIndex - b.orderIndex))

function checkStation(data: AdminDb, input: NewStation, ignoreId?: string) {
  if (data.stations.some((s) => s.id !== ignoreId && s.code.toLowerCase() === input.code.toLowerCase())) {
    throw conflict(t('A station with this code already exists.'))
  }
}

export async function createStation(input: NewStation): Promise<AdminStation> {
  const data = await db()
  checkStation(data, input)
  const station: AdminStation = { ...input, id: newId(), isActive: true }
  data.stations.push(station)
  return withDelay(station)
}

export async function updateStation(id: string, input: NewStation): Promise<AdminStation> {
  const data = await db()
  const station = data.stations.find((s) => s.id === id)
  if (!station) throw notFound()
  checkStation(data, input, id)
  Object.assign(station, input)
  return withDelay(station)
}

/** Soft delete. A station that an active route still uses cannot be removed. */
export async function deleteStation(id: string): Promise<void> {
  const data = await db()
  if (data.routes.some((r) => r.isActive && (r.departureStationId === id || r.arrivalStationId === id))) {
    throw conflict(t('This station is used by an active route.'))
  }
  data.stations = data.stations.filter((s) => s.id !== id)
  await withDelay(null)
}

// ---- Routes -------------------------------------------------------------------

export const getRoutes = () => query((d) => d.routes)

// ---- Boats --------------------------------------------------------------------

export const getBoats = () => query((d) => d.boats.filter((b) => b.isActive))

export async function createBoat(input: NewBoat): Promise<AdminBoat> {
  const data = await db()
  if (data.boats.some((b) => b.code.toLowerCase() === input.code.toLowerCase())) {
    throw conflict(t('A boat with this code already exists.'))
  }
  const boat: AdminBoat = { ...input, id: newId(), totalSeats: totalSeatsOf(input), isActive: true }
  data.boats.push(boat)
  return withDelay(boat)
}

/** Soft delete. A boat with trips still to run cannot be removed. */
export async function deleteBoat(id: string): Promise<void> {
  const data = await db()
  if (data.trips.some((trip) => trip.boatId === id && !['Completed', 'Cancelled'].includes(trip.status))) {
    throw conflict(t('This boat still has trips to run.'))
  }
  const boat = data.boats.find((b) => b.id === id)
  if (boat) boat.isActive = false
  await withDelay(null)
}

// ---- Fares --------------------------------------------------------------------

export const getFares = () => query((d) => d.fares)

export async function saveFares(fares: FareSettings): Promise<FareSettings> {
  const data = await db()
  data.fares = fares
  return withDelay(fares)
}

// ---- Schedules and trips ------------------------------------------------------

export const getSchedules = () => query((d) => d.schedules)

export async function createSchedule(input: NewSchedule): Promise<AdminSchedule> {
  const data = await db()
  if (!data.routes.some((r) => r.id === input.routeId)) throw notFound()
  const schedule: AdminSchedule = { ...input, id: newId(), isActive: true }
  data.schedules.push(schedule)
  return withDelay(schedule)
}

export async function setScheduleActive(id: string, isActive: boolean): Promise<void> {
  const data = await db()
  const schedule = data.schedules.find((s) => s.id === id)
  if (!schedule) throw notFound()
  schedule.isActive = isActive
  await withDelay(null)
}

/**
 * Creates the trips of a schedule for each matching day in the range, on the chosen boat.
 * Running it twice for the same range creates nothing new (a trip is unique per schedule and
 * departure time). `skipped` counts days that already had their trip.
 */
export async function generateTrips(
  input: GenerateTripsInput,
): Promise<{ created: number; skipped: number }> {
  const data = await db()
  const schedule = data.schedules.find((s) => s.id === input.scheduleId)
  const route = data.routes.find((r) => r.id === schedule?.routeId)
  const boat = data.boats.find((b) => b.id === input.boatId && b.isActive)
  if (!schedule || !route || !boat) throw notFound()

  const from = new Date(`${input.fromDate}T00:00:00`)
  const to = new Date(`${input.toDate}T00:00:00`)
  const days = Math.round((to.getTime() - from.getTime()) / 86_400_000) + 1
  if (!(days >= 1)) throw new ApiError(400, t('The end date must not be before the start date.'))
  if (days > MAX_GENERATE_DAYS) {
    throw new ApiError(400, t('Generate at most {days} days at a time.', { days: MAX_GENERATE_DAYS }))
  }

  const [hours, minutes] = schedule.departureTime.split(':').map(Number)
  const toAdd: AdminTrip[] = []
  let skipped = 0
  for (let offset = 0; offset < days; offset++) {
    const day = new Date(from.getFullYear(), from.getMonth(), from.getDate() + offset)
    if (!schedule.daysOfWeek.includes(backendDayOf(day))) continue
    const departure = new Date(day.getFullYear(), day.getMonth(), day.getDate(), hours, minutes)
    const departureTime = departure.toISOString()
    if (data.trips.some((trip) => trip.scheduleId === schedule.id && trip.departureTime === departureTime)) {
      skipped++
      continue
    }
    const trip: AdminTrip = {
      id: newId(),
      scheduleId: schedule.id,
      routeId: route.id,
      boatId: boat.id,
      departureTime,
      arrivalTime: new Date(departure.getTime() + route.estimatedDurationMinutes * MINUTE).toISOString(),
      tripType: schedule.tripType,
      status: 'Scheduled',
      basePrice: data.fares.basePrice[schedule.tripType],
    }
    if (data.trips.some((other) => other.boatId === boat.id && other.status !== 'Cancelled' && overlaps(trip, other))) {
      throw conflict(
        t('{boat} already has a trip at {time} on {date}.', {
          boat: boat.code,
          time: schedule.departureTime,
          date: toIsoDate(day),
        }),
      )
    }
    toAdd.push(trip)
  }
  // Nothing is created unless every day in the range is free, so a failed run changes nothing.
  data.trips.push(...toAdd)
  return withDelay({ created: toAdd.length, skipped })
}

export const getTrips = () =>
  query((d) => [...d.trips].sort((a, b) => a.departureTime.localeCompare(b.departureTime)))

/** Puts a different boat on a trip that has not started. The boat must be free at that time. */
export async function assignBoat(tripId: string, boatId: string): Promise<AdminTrip> {
  const data = await db()
  const trip = data.trips.find((x) => x.id === tripId)
  const boat = data.boats.find((b) => b.id === boatId && b.isActive)
  if (!trip || !boat) throw notFound()
  if (!['Scheduled', 'Boarding', 'Delayed'].includes(trip.status)) {
    throw conflict(t('The boat of a trip that has started or ended cannot be changed.'))
  }
  if (data.trips.some((other) => other.id !== tripId && other.boatId === boatId && other.status !== 'Cancelled' && overlaps(trip, other))) {
    throw conflict(t('{boat} is already on another trip at that time.', { boat: boat.code }))
  }
  trip.boatId = boatId
  return withDelay(trip)
}

// ---- Operations -----------------------------------------------------------------

export const getWeatherAlerts = () => query((d) => d.alerts)

export const getOperationLogs = () => query((d) => [...d.logs].sort((a, b) => b.at.localeCompare(a.at)))

/**
 * Applies a dispatcher or captain operation to a trip and records it. Rejects with ApiError(409)
 * when the trip's status does not allow it (for example cancelling a trip that already ended).
 */
export async function operateTrip(
  tripId: string,
  type: OperationType,
  reason: string,
  by: string,
): Promise<AdminTrip> {
  const data = await db()
  const trip = data.trips.find((x) => x.id === tripId)
  if (!trip) throw notFound()
  if (type !== 'SOS' && !allowedOperations(trip.status).includes(type)) {
    throw conflict(t('This trip cannot be changed from its current status.'))
  }
  const result = OPERATION_RESULT[type]
  if (result) trip.status = result
  data.logs.push({ id: newId(), type, tripId, reason, by, at: new Date().toISOString() })
  return withDelay(trip)
}

// ---- Radar --------------------------------------------------------------------

/**
 * Mock of the tracking hub (roadmap P3-BE-01, which will receive GPS from the boats): the position
 * of every boat on the river is worked out from where its trip should be at this moment.
 */
export async function getFleetPositions(): Promise<FleetPosition[]> {
  const data = await db()
  const now = Date.now()
  const positions = data.trips
    .filter((trip) => trip.status === 'InTransit')
    .flatMap((trip) => {
      const route = data.routes.find((r) => r.id === trip.routeId)
      if (!route) return []
      const points = routeStations(route, data.stations).map((s) => [s.latitude, s.longitude] as [number, number])
      const progress = tripProgress(trip, now)
      const [latitude, longitude] = positionAlong(points, progress)
      const hours = route.estimatedDurationMinutes / 60
      return [
        {
          tripId: trip.id,
          boatId: trip.boatId,
          latitude,
          longitude,
          progress,
          speedKmh: Math.round((route.distanceKm / hours) * 10) / 10,
        },
      ]
    })
  return withDelay(positions, 0)
}
