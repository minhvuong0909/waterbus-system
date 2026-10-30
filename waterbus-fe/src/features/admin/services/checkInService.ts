import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { TripDetailDto, TripStopDto } from '../../trips/types'

/** The stops of a trip in visit order, with their check-in windows (`GET /trips/{id}`). */
export async function getTripStops(tripId: string): Promise<TripStopDto[]> {
  const trip = await apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId))
  return [...trip.stops].sort((a, b) => a.visitOrder - b.visitOrder)
}

/** Response of `PUT /checkins/windows/{id}` (BE `SetCheckInWindowResultDto`). */
export interface CheckInWindow {
  tripStopCallId: string
  checkInOpenAt: string
  checkInCloseAt: string
}

/**
 * Sets when staff may check passengers in at one stop (BR-BOARD-05: the window belongs to the stop
 * where they board). The backend refuses a stop that has departed or was skipped.
 */
export function setCheckInWindow(tripStopCallId: string, openAt: string, closeAt: string): Promise<CheckInWindow> {
  return apiRequest<CheckInWindow>(API_ENDPOINTS.checkIns.window(tripStopCallId), {
    method: 'PUT',
    body: { tripStopCallId, checkInOpenAt: openAt, checkInCloseAt: closeAt },
  })
}
