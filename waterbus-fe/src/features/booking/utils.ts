import { t } from '../../i18n'
import type { TripDetail } from '../trips/types'
import type { PassengerForm, Seat, SeatCategory, Voucher } from './types'

/** Cabin names, in the order they appear on the boat. */
export const CABIN_ORDER = ['front', 'standard', 'outdoor'] as const

const CABIN_LABEL: Record<SeatCategory, string> = {
  front: 'Front Cabin',
  standard: 'Standard Cabin',
  outdoor: 'Outdoor Deck',
}

export function cabinLabel(category: SeatCategory): string {
  return t(CABIN_LABEL[category])
}

/** Code printed on the seat. Falls back to `seatId` while the seat map is not loaded. */
export function seatCode(seat: Seat | undefined, seatId: string): string {
  return seat?.code ?? seatId
}

/** Extra charge of the seat: the fare times (multiplier - 1), so 0 for a standard seat. */
export function seatFeeFor(seat: Seat | undefined, fareVnd: number): number {
  return seat ? Math.round(fareVnd * (seat.priceMultiplier - 1)) : 0
}

export interface BookingTotals {
  fareVnd: number
  /** Cabin surcharge, 0 for a standard seat. */
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

/** True for a seat that costs more than the base fare. */
export const hasSurcharge = (seat: Seat | undefined): boolean => (seat?.priceMultiplier ?? 1) > 1

/** "Front Cabin • Window", "Window", "Outdoor Deck" or "" (a standard aisle seat). */
export function seatTags(seat: Seat | undefined): string {
  return [
    seat && seat.category !== 'standard' && cabinLabel(seat.category),
    seat?.window && t('Window'),
  ]
    .filter(Boolean)
    .join(' • ')
}

/** "Seat S01 (Window)", "Seat F03 (Front Cabin • Window)" or "Seat S02". */
export function seatLabel(seat: Seat | undefined, seatId: string): string {
  const id = seatCode(seat, seatId)
  const tags = seatTags(seat)
  return tags ? t('Seat {id} ({tags})', { id, tags }) : t('Seat {id}', { id })
}

/** "Portside Window", "Starboard Aisle"... */
export function seatPosition(seat: Seat | undefined): string {
  if (!seat) return t('Main Saloon')
  const side = seat.column === 'A' || seat.column === 'B' ? 'Portside' : 'Starboard'
  const kind = seat.window ? 'Window' : 'Aisle'
  return t(`${side} ${kind}`)
}

/** Cabin the seat is in, e.g. "Front Cabin"; "Main Saloon" until the seat is known. */
export function seatZone(seat: Seat | undefined): string {
  return seat ? cabinLabel(seat.category) : t('Main Saloon')
}

/** Mock booking reference, e.g. WB-2025-0842-A2. Real references will come from the backend. */
export function bookingReference(seatId: string): string {
  return `WB-2025-0842-${seatId}`
}

/** Phone number as the backend expects it: digits only, with a leading 0 instead of "+84". */
export function toCustomerPhone(passenger: Pick<PassengerForm, 'phone'>): string {
  const digits = passenger.phone.replace(/\D/g, '')
  return digits.startsWith('0') ? digits : `0${digits}`
}
