import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { TripDetailDto } from '../../booking/types'
import { getPierOptions } from '../../trips/services/tripService'
import type { TrackedTrip } from '../types'

/**
 * `GET /trips/{id}` (public) with each stop placed at its station from `GET /stations`. Stops whose
 * station is no longer listed are left off the map rather than drawn at a made-up position.
 */
export async function getTrackedTrip(tripId: string): Promise<TrackedTrip> {
  const [trip, piers] = await Promise.all([
    apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId)),
    getPierOptions(),
  ])
  const stops = [...trip.stops]
    .sort((a, b) => a.visitOrder - b.visitOrder)
    .flatMap((stop) => {
      const pier = piers.find((p) => p.id === stop.stationId)
      if (!pier) return []
      return [{
        callId: stop.callId,
        stationName: stop.stationName,
        visitOrder: stop.visitOrder,
        latitude: pier.latitude,
        longitude: pier.longitude,
        status: stop.status,
        estimatedArrivalTime: stop.estimatedArrivalTime ?? null,
        estimatedDepartureTime: stop.estimatedDepartureTime ?? null,
      }]
    })
  return {
    tripId: trip.tripId,
    routeName: trip.routeName,
    status: trip.status as TrackedTrip['status'],
    boat: trip.boat,
    departureTime: trip.departureTime,
    arrivalTime: trip.arrivalTime,
    stops,
  }
}
