import { SEAT_MAP, VALID_VOUCHER } from '../../../mocks/booking'
import { withDelay } from '../../../mocks/delay'
import type { SeatMap, Voucher } from '../types'

// Mock: the backend has no seat map endpoint yet (roadmap P1-BE-05). `SEAT_MAP` follows the boat
// layout the backend seeds, and a real response will use the same `SeatMap` shape.
export function getSeatMap(): Promise<SeatMap> {
  return withDelay(SEAT_MAP)
}

/** Resolves with the voucher when valid, or null. The result never blocks the flow. */
export function validateVoucher(code: string): Promise<Voucher | null> {
  const normalized = code.trim().toUpperCase()
  if (normalized !== VALID_VOUCHER.code) return withDelay(null, 200)
  return withDelay({ code: normalized, percentOff: VALID_VOUCHER.percentOff }, 200)
}
