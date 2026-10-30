import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { t } from '../../../i18n'
import { ApiError, apiRequest } from '../../../services/apiClient'
import type {
  CheckoutDetail,
  CheckoutResponse,
  CreateHoldRequest,
  SeatMap,
  SubmitCheckoutRequest,
  TripDetailDto,
  TripSeatMapDto,
} from '../types'
import { isGuid, pickJourney, toSeatMap } from '../utils'

/** Header that proves the caller owns a guest order (BE `CheckoutAccessService`). */
export const guestHeaders = (accessToken: string | null): Record<string, string> =>
  accessToken ? { 'X-Guest-Token': accessToken } : {}

/** True when the ids of a trip and its piers are backend ids (Guids). */
export function isLiveTrip(tripId: string | null, originId: string, destinationId: string): boolean {
  return tripId !== null && [tripId, originId, destinationId].every(isGuid)
}

/**
 * Seat map of the trip for the chosen piers. A backend trip loads `GET /trips/{id}` (to find the
 * real boarding and alighting stops) and then `GET /trips/{id}/seats` for that segment, with the
 * fare and availability of each seat. Ids that are not Guids are rejected.
 */
export async function getSeatMap(
  tripId: string | null,
  originId: string,
  destinationId: string,
): Promise<SeatMap> {
  if (!isLiveTrip(tripId, originId, destinationId) || tripId === null) {
    throw new ApiError(400, 'Vui lòng chọn chuyến và hai bến từ kết quả tìm kiếm.')
  }
  const detail = await apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId))
  const journey = pickJourney(detail.stops, originId, destinationId)
  if (!journey) {
    throw new ApiError(409, t('This trip does not stop at both of the chosen piers.'))
  }
  const dto = await apiRequest<TripSeatMapDto>(API_ENDPOINTS.trips.seats(tripId), {
    query: {
      boardingCallId: journey.boarding.callId,
      disembarkingCallId: journey.disembarking.callId,
    },
  })
  return toSeatMap(dto, detail)
}

/**
 * `POST /checkouts/holds`: holds the seats before the buyer is known (BR-HOLD). The order stays a
 * Draft until it is submitted. Rejects with ApiError(409) when a seat is already held or sold.
 * The response carries the hold deadline (`expiresAt`) and the guest token of the order, which is
 * only returned here.
 */
export function createHold(request: CreateHoldRequest): Promise<CheckoutResponse> {
  const live = request.legs.length > 0 &&
    request.legs.every((leg) => isLiveTrip(leg.tripId, leg.boardingStationId, leg.disembarkingStationId))
  if (!live) {
    return Promise.reject(new ApiError(400, 'Chuyến và bến phải lấy từ API tìm chuyến.'))
  }
  return apiRequest<CheckoutResponse>(API_ENDPOINTS.checkouts.holds, {
    method: 'POST',
    body: request,
  })
}

/**
 * `POST /checkouts/{orderId}/submit`: gives the held order its buyer and passengers and makes it
 * payable (PendingPayment). No ticket is issued yet (BR-COM-08).
 */
export async function submitCheckout(
  orderId: string,
  accessToken: string | null,
  request: SubmitCheckoutRequest,
): Promise<void> {
  await apiRequest<unknown>(API_ENDPOINTS.checkouts.submit(orderId), {
    method: 'POST',
    body: request,
    headers: guestHeaders(accessToken),
  })
}

/**
 * `GET /checkouts/{orderId}`: the order as the backend knows it. Its `status` is the only proof
 * of payment (`Confirmed`), set when VNPay's IPN is verified, never by the browser.
 */
export function getCheckout(orderId: string, accessToken: string | null): Promise<CheckoutDetail> {
  return apiRequest<CheckoutDetail>(API_ENDPOINTS.checkouts.byId(orderId), {
    headers: guestHeaders(accessToken),
  })
}

/** Credential of a paid booking (BE `BookingQrCredential`). */
export interface BookingQr {
  /** Compact RS256 JWS the Staff Scanner verifies offline; this is what the QR encodes. */
  payload: string
  algorithm: string
  keyId: string
  credentialVersion: number
}

/**
 * `GET /checkouts/{orderId}/bookings/{bookingId}/qr`: the boarding QR of a booking, available
 * only after payment is confirmed.
 */
export function getBookingQr(
  orderId: string,
  bookingId: string,
  accessToken: string | null,
): Promise<BookingQr> {
  return apiRequest<BookingQr>(API_ENDPOINTS.checkouts.qr(orderId, bookingId), {
    headers: guestHeaders(accessToken),
  })
}

/** `DELETE /checkouts/{orderId}`: releases the held seats. Best effort: a failure is ignored. */
export async function cancelCheckout(orderId: string, accessToken: string | null): Promise<void> {
  try {
    await apiRequest<unknown>(API_ENDPOINTS.checkouts.byId(orderId), {
      method: 'DELETE',
      headers: guestHeaders(accessToken),
    })
  } catch {
    // The hold ends by itself when it expires.
  }
}
