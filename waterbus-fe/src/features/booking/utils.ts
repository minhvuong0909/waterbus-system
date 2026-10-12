import { t } from '../../i18n'
import type { TripDetail } from '../trips/types'
import type { Seat, Voucher } from './types'

/** VIP seats cost this share of the trip fare on top of it (15,000 VND fare -> 9,000 VND extra). */
const VIP_SURCHARGE_RATE = 0.6
const VND_STEP = 1000

/** Extra charge for the seat's tier. Standard seats are free to reserve. */
export function seatFeeFor(seat: Seat | undefined, fareVnd: number): number {
  if (seat?.tier !== 'vip') return 0
  return Math.round((fareVnd * VIP_SURCHARGE_RATE) / VND_STEP) * VND_STEP
}

export interface BookingTotals {
  fareVnd: number
  /** VIP surcharge, 0 for a standard seat. */
  seatFeeVnd: number
  discountVnd: number
  totalVnd: number
}

/** The voucher discounts the trip fare only, never the seat surcharge. */
export function computeTotals(
  trip: TripDetail,
  seat: Seat | undefined,
  voucher: Voucher | null,
): BookingTotals {
  const seatFeeVnd = seatFeeFor(seat, trip.fareVnd)
  const discountVnd = voucher ? Math.round((trip.fareVnd * voucher.percentOff) / 100) : 0
  return {
    fareVnd: trip.fareVnd,
    seatFeeVnd,
    discountVnd,
    totalVnd: Math.max(trip.fareVnd - discountVnd, 0) + seatFeeVnd,
  }
}

export const isVip = (seat: Seat | undefined): boolean => seat?.tier === 'vip'

/** "VIP • Window", "Window", "VIP" or "" (empty for a standard aisle seat). */
export function seatTags(seat: Seat | undefined): string {
  return [isVip(seat) && t('VIP'), seat?.window && t('Window')].filter(Boolean).join(' • ')
}

/** "Seat A2 (Window)", "Seat A5 (VIP • Window)" or "Seat B3". */
export function seatLabel(seat: Seat | undefined, seatId: string): string {
  const tags = seatTags(seat)
  return tags ? t('Seat {id} ({tags})', { id: seatId, tags }) : t('Seat {id}', { id: seatId })
}

/** "Portside Window", "Starboard Aisle"... */
export function seatPosition(seat: Seat | undefined): string {
  if (!seat) return t('Main Saloon')
  const side = seat.column === 'A' || seat.column === 'B' ? 'Portside' : 'Starboard'
  const kind = seat.window ? 'Window' : 'Aisle'
  return t(`${side} ${kind}`)
}

/** Where the seat is on the vessel: "VIP Lounge" or "Main Deck". */
export function seatZone(seat: Seat | undefined): string {
  return isVip(seat) ? t('VIP Lounge') : t('Main Deck')
}

/** Cabin class shown next to the fare: "VIP Class" or "Eco Class". */
export function seatClassLabel(seat: Seat | undefined): string {
  return isVip(seat) ? t('VIP Class') : t('Eco Class')
}

/** Mock booking reference, e.g. WB-2025-0842-A2. Real references will come from the backend. */
export function bookingReference(seatId: string): string {
  return `WB-2025-0842-${seatId}`
}
