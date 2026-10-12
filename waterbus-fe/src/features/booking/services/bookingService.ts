import { SEAT_MAP, VALID_VOUCHER } from '../../../mocks/booking'
import { withDelay } from '../../../mocks/delay'
import type { SeatMap, Voucher } from '../types'

// Mock service layer: swap for real API calls later without touching the UI.
export function getSeatMap(): Promise<SeatMap> {
  return withDelay(SEAT_MAP)
}

/** Resolves with the voucher when valid, or null. The result never blocks the flow. */
export function validateVoucher(code: string): Promise<Voucher | null> {
  const normalized = code.trim().toUpperCase()
  if (normalized !== VALID_VOUCHER.code) return withDelay(null, 200)
  return withDelay({ code: normalized, percentOff: VALID_VOUCHER.percentOff }, 200)
}
