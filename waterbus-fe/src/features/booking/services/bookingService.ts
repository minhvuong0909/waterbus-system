import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { SEAT_MAP } from '../../../mocks/booking'
import { withDelayExact } from '../../../mocks/delay'
import { apiRequest } from '../../../services/apiClient'
import type { BookingResponse, CreateBookingRequest, SeatMap } from '../types'

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
const HOLD_SECONDS = 600

// Mock: the backend has no seat map endpoint yet (roadmap P1-BE-05). `SEAT_MAP` follows the boat
// layout the backend seeds, and a real response will use the same `SeatMap` shape.
export function getSeatMap(): Promise<SeatMap> {
  // Exact: seat statuses such as "sold" are values the UI compares, not text to translate.
  return withDelayExact(SEAT_MAP)
}

/**
 * `POST /bookings`: books the seats and holds them for 10 minutes. Rejects with ApiError(409)
 * when a seat is already held or sold, and ApiError(400) with `fieldErrors` for invalid details.
 *
 * Seat ids are Guids only once the seat map comes from the backend; the mock seat map uses seat
 * codes, so until then (or with `VITE_USE_MOCK`) this returns a mock booking worth `quotedTotalVnd`.
 */
export function createBooking(
  request: CreateBookingRequest,
  quotedTotalVnd: number,
): Promise<BookingResponse> {
  if (!USE_MOCK && request.seatIds.every((id) => GUID.test(id))) {
    return apiRequest<BookingResponse>(API_ENDPOINTS.bookings, { method: 'POST', body: request })
  }
  const stamp = new Date().toISOString().replace(/\D/g, '').slice(0, 14)
  return withDelayExact({
    bookingId: crypto.randomUUID(),
    bookingCode: `WB${stamp}${Math.floor(100 + Math.random() * 900)}`,
    totalAmount: quotedTotalVnd,
    status: 'Pending',
    holdDurationSeconds: HOLD_SECONDS,
  })
}
