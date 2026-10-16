import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { SEAT_MAP } from '../../../mocks/booking'
import { withDelayExact } from '../../../mocks/delay'
import { apiRequest } from '../../../services/apiClient'
import type { CheckoutDetail, CheckoutResponse, CreateCheckoutRequest, SeatMap } from '../types'
import { isGuid } from '../utils'

const HOLD_SECONDS = 600

/** Header that proves the caller owns a guest order (BE `CheckoutAccessService`). */
export const guestHeaders = (accessToken: string | null): Record<string, string> =>
  accessToken ? { 'X-Guest-Token': accessToken } : {}

// Mock: the backend has no seat map endpoint yet (roadmap P1-BE-05). `SEAT_MAP` follows the boat
// layout the backend seeds, and a real response will use the same `SeatMap` shape.
export function getSeatMap(): Promise<SeatMap> {
  // Exact: seat statuses such as "sold" are values the UI compares, not text to translate.
  return withDelayExact(SEAT_MAP)
}

/**
 * True when the checkout can go to the real backend: not in mock mode, and the trip, stations and
 * seats are all backend Guids. The mock seat map uses seat codes (no seat endpoint yet), so until
 * the seat map is real, bookings made from it are mock ones.
 */
export function canUseLiveCheckout(request: CreateCheckoutRequest): boolean {
  return (
    !USE_MOCK &&
    request.legs.every(
      (leg) =>
        isGuid(leg.tripId) &&
        isGuid(leg.boardingStationId) &&
        isGuid(leg.disembarkingStationId) &&
        leg.seatIds.every(isGuid),
    )
  )
}

/**
 * `POST /checkouts`: creates the order, one booking per trip, and holds the seats (BR-HOLD).
 * Rejects with ApiError(409) when a seat is already held or sold, and ApiError(400) with
 * `fieldErrors` for invalid details. No ticket exists yet: tickets are issued only after the
 * backend verifies the payment (BR-PAY-01).
 *
 * MOCK (see `canUseLiveCheckout`): returns an order worth `quotedTotalVnd` without any request.
 */
export function createCheckout(
  request: CreateCheckoutRequest,
  quotedTotalVnd: number,
): Promise<CheckoutResponse> {
  if (canUseLiveCheckout(request)) {
    return apiRequest<CheckoutResponse>(API_ENDPOINTS.checkouts.create, {
      method: 'POST',
      body: request,
    })
  }
  const stamp = new Date().toISOString().replace(/\D/g, '').slice(0, 14)
  const orderId = crypto.randomUUID()
  return withDelayExact({
    orderId,
    quotedTotal: quotedTotalVnd,
    currency: 'VND',
    status: 'PendingPayment',
    expiresAt: new Date(Date.now() + HOLD_SECONDS * 1000).toISOString(),
    accessToken: null,
    bookings: [
      {
        bookingId: crypto.randomUUID(),
        bookingCode: `WB${stamp}${Math.floor(100 + Math.random() * 900)}`,
        totalAmount: quotedTotalVnd,
        status: 'PendingPayment',
        holdDurationSeconds: HOLD_SECONDS,
        orderId,
        orderStatus: 'PendingPayment',
      },
    ],
  })
}

/**
 * `GET /checkouts/{orderId}`: the order as the backend knows it. Its `status` is the only proof
 * of payment (`Confirmed`), set when VNPay's IPN is verified, never by the browser.
 * MOCK (no token, i.e. a mock order): reports `Confirmed`.
 */
export function getCheckout(orderId: string, accessToken: string | null): Promise<CheckoutDetail> {
  if (!accessToken) {
    return withDelayExact({
      orderId,
      status: 'Confirmed' as const,
      quotedTotal: 0,
      expiresAt: null,
      bookings: [],
      payments: [],
    })
  }
  return apiRequest<CheckoutDetail>(API_ENDPOINTS.checkouts.byId(orderId), {
    headers: guestHeaders(accessToken),
  })
}

/** `DELETE /checkouts/{orderId}`: releases the held seats. Best effort: a failure is ignored. */
export async function cancelCheckout(orderId: string, accessToken: string | null): Promise<void> {
  if (!accessToken) return
  try {
    await apiRequest<unknown>(API_ENDPOINTS.checkouts.byId(orderId), {
      method: 'DELETE',
      headers: guestHeaders(accessToken),
    })
  } catch {
    // The hold ends by itself when it expires.
  }
}
