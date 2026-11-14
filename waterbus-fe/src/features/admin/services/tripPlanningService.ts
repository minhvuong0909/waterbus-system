import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdHocTripInput, AssignBoatResult, GenerateTripsInput, GenerateTripsResult } from '../types'

// New trips start `Scheduled` and without a boat, so they are not on sale until one is assigned
// (BR-SCH-04). Status changes stay with the operations service.

/**
 * `POST /admin/trips/generate`: creates the trips of a schedule for a range of days. Days that
 * already have the trip, are in the past or are not operating days are skipped, so it can be run
 * again safely.
 */
export const generateTrips = (input: GenerateTripsInput) =>
  apiRequest<GenerateTripsResult>(API_ENDPOINTS.admin.generateTrips, { method: 'POST', body: input })

/** `POST /admin/trips` (BR-SCH-10): one trip outside any schedule, on a route and its stops. */
export const createAdHocTrip = (input: AdHocTripInput) =>
  apiRequest<{ tripId: string; departureTime: string }>(API_ENDPOINTS.admin.trips, {
    method: 'POST',
    body: input,
  })

/**
 * `PUT /admin/trips/{tripId}/boat`: gives a `Scheduled` trip its boat or replaces it. The backend
 * refuses (409) a boat already running an overlapping trip (BR-SCH-08) or one that lacks a seat
 * of the same code and class for a seat already sold (BR-SCH-07); sold seats are moved over.
 */
export const assignTripBoat = (tripId: string, boatId: string) =>
  apiRequest<AssignBoatResult>(API_ENDPOINTS.admin.tripBoat(tripId), { method: 'PUT', body: { boatId } })
