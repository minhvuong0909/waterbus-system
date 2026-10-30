import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { clearPierCache } from '../../trips/services/tripService'
import type { StationDto } from '../../trips/types'
import type {
  AdminBoat,
  AdminRoute,
  AdminStation,
  AdminTrip,
  AdminTripStatus,
  NewBoat,
  NewStation,
  OperationLog,
  OperationType,
  WeatherAlert,
} from '../types'
import { totalSeatsOf } from '../utils'
import {
  getLiveOperationLogs,
  getLiveOperationTrips,
  getOpenIncidents,
  operateLiveTrip,
  resolveIncident,
} from './operationsService'

// Bến, tàu, chuyến và vận hành của khu Admin/Captain, đều gọi API thật.

// ---- Stations -----------------------------------------------------------------

export const getStations = () => apiRequest<StationDto[]>(API_ENDPOINTS.stations)

export async function createStation(input: NewStation): Promise<AdminStation> {
  const id = await apiRequest<string>(API_ENDPOINTS.stations, { method: 'POST', body: input })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}

export async function updateStation(id: string, input: NewStation): Promise<AdminStation> {
  const current = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...input, id, isActive: current.isActive } })
  clearPierCache()
  return apiRequest<AdminStation>(API_ENDPOINTS.station(id))
}

/** Soft delete: the backend has no DELETE, so the station is deactivated. */
export async function deleteStation(id: string): Promise<void> {
  const station = await apiRequest<AdminStation>(API_ENDPOINTS.station(id))
  await apiRequest(API_ENDPOINTS.station(id), { method: 'PUT', body: { ...station, isActive: false } })
  clearPierCache()
}

// ---- Routes -------------------------------------------------------------------

export const getRoutes = () => Promise.resolve([] as AdminRoute[])

// ---- Boats --------------------------------------------------------------------

interface BoatApiDto extends Omit<AdminBoat, 'frontCabinSeats' | 'standardSeats' | 'outdoorSeats'> {
  captainUserId: string | null
}

export async function getBoats(): Promise<AdminBoat[]> {
  const boats: AdminBoat[] = []
  for (let pageNumber = 1; ; pageNumber++) {
    const page = await apiRequest<{ items: BoatApiDto[]; totalCount: number }>(API_ENDPOINTS.boats, {
      query: { pageNumber, pageSize: 100, isActiveOnly: true },
    })
    boats.push(...page.items.map((boat) => ({ ...boat, frontCabinSeats: 0, standardSeats: boat.totalSeats, outdoorSeats: 0 })))
    if (!page.items.length || boats.length >= page.totalCount) return boats
  }
}

export async function createBoat(input: NewBoat): Promise<AdminBoat> {
  const id = await apiRequest<string>(API_ENDPOINTS.boats, {
    method: 'POST', body: { ...input, totalSeats: totalSeatsOf(input), captainUserId: null },
  })
  const boat = await apiRequest<BoatApiDto>(API_ENDPOINTS.boat(id))
  return { ...boat, frontCabinSeats: 0, standardSeats: boat.totalSeats, outdoorSeats: 0 }
}

/** Soft delete: the backend has no DELETE, so the boat is deactivated. */
export async function deleteBoat(id: string): Promise<void> {
  const boat = await apiRequest<BoatApiDto>(API_ENDPOINTS.boat(id))
  await apiRequest(API_ENDPOINTS.boat(id), { method: 'PUT', body: { ...boat, isActive: false } })
}

// ---- Trips -------------------------------------------------------------------

export const getTrips = (): Promise<AdminTrip[]> => getLiveOperationTrips()

// ---- Operations ---------------------------------------------------------------

/** Open incidents (weather guard, captain SOS). */
export const getWeatherAlerts = (): Promise<WeatherAlert[]> => getOpenIncidents()

/** Open incidents with the trips each one affects and the admin's decision per trip. */
export const getAlertsWithTrips = (): Promise<WeatherAlert[]> => getOpenIncidents(true)

/** Closes an open incident without changing any trip. */
export async function closeAlert(incidentId: string): Promise<void> {
  await resolveIncident(incidentId)
}

/** Trips to operate (Incidents, Captain Portal, Overview). */
export const getOperationTrips = (): Promise<AdminTrip[]> => getLiveOperationTrips()

export const getOperationLogs = (): Promise<OperationLog[]> => getLiveOperationLogs()

/** Applies an operation to a trip through the backend, which records it and decides the status. */
export async function operateTrip(
  tripId: string,
  type: OperationType,
  reason: string,
  _by: string,
  incidentId?: string,
): Promise<{ status: AdminTripStatus | null }> {
  return operateLiveTrip(tripId, type, reason, incidentId)
}
