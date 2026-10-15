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

/** Seats one booking may hold (the backend's CreateBookingCommandValidator allows 10). */
export const MAX_SEATS_PER_BOOKING = 10

/** "S01, F03": the codes printed on the seats. */
export function seatCodes(seats: Seat[]): string {
  return seats.map((s) => s.code).join(', ')
}

/** "1 Passenger" / "3 Passengers": there is one passenger per seat. */
export function passengersLabel(count: number): string {
  return count === 1 ? t('1 Passenger') : t('{count} Passengers', { count })
}

/** "Front Cabin / Standard Cabin": the cabins the seats are in, each named once. */
export function cabinsLabel(seats: Seat[]): string {
  const categories = CABIN_ORDER.filter((c) => seats.some((s) => s.category === c))
  return categories.length > 0 ? categories.map(cabinLabel).join(' / ') : t('Main Saloon')
}

/** Extra charge of the seat: the fare times (multiplier - 1), so 0 for a standard seat. */
export function seatFeeFor(seat: Seat | undefined, fareVnd: number): number {
  return seat ? Math.round(fareVnd * (seat.priceMultiplier - 1)) : 0
}

export interface BookingTotals {
  seatCount: number
  /** Trip fare for all seats (the backend charges the base price once per seat). */
  fareVnd: number
  /** Cabin surcharges of all seats, 0 when every seat is standard. */
  seatFeeVnd: number
  discountVnd: number
  totalVnd: number
}

/**
 * What each ticket costs: the trip fare plus that seat's cabin surcharge, minus an equal share of
 * the voucher discount (the voucher takes a percentage off the trip fare, never off a surcharge).
 * The amounts add up exactly to the booking total.
 */
export function ticketFares(fareVnd: number, seats: Seat[], voucher: Voucher | null): number[] {
  if (seats.length === 0) return []
  const discountVnd = voucher ? Math.round((fareVnd * seats.length * voucher.percentOff) / 100) : 0
  const share = Math.floor(discountVnd / seats.length)
  return seats.map((seat, index) => {
    // The first seat takes the rounding remainder, so no dong is lost.
    const discount = share + (index === 0 ? discountVnd - share * seats.length : 0)
    return Math.max(fareVnd + seatFeeFor(seat, fareVnd) - discount, 0)
  })
}

export function computeTotals(
  trip: TripDetail,
  seats: Seat[],
  voucher: Voucher | null,
): BookingTotals {
  const fareVnd = trip.fareVnd * seats.length
  const seatFeeVnd = seats.reduce((sum, seat) => sum + seatFeeFor(seat, trip.fareVnd), 0)
  const discountVnd = voucher ? Math.round((fareVnd * voucher.percentOff) / 100) : 0
  return {
    seatCount: seats.length,
    fareVnd,
    seatFeeVnd,
    discountVnd,
    totalVnd: Math.max(fareVnd - discountVnd, 0) + seatFeeVnd,
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

/** "Seat S01 (Window)" for one seat, "Seats S01, F03" for several. */
export function selectedSeatsLabel(seats: Seat[]): string {
  if (seats.length === 1) {
    const tags = seatTags(seats[0])
    return tags
      ? t('Seat {id} ({tags})', { id: seats[0].code, tags })
      : t('Seat {id}', { id: seats[0].code })
  }
  return t('Seats {ids}', { ids: seatCodes(seats) })
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

/** Mock booking reference in the backend's format (WB + date and time + 3 digits). The backend issues the real one. */
export function bookingReference(seats: Seat[]): string {
  const digits = seatCodes(seats).replace(/\D/g, '').padStart(3, '0').slice(-3)
  return `WB${new Date().toISOString().replace(/\D/g, '').slice(0, 8)}0000${digits}`
}

/** Phone number as the backend expects it: digits only, with a leading 0 instead of "+84". */
export function toCustomerPhone(passenger: Pick<PassengerForm, 'phone'>): string {
  const digits = passenger.phone.replace(/\D/g, '')
  return digits.startsWith('0') ? digits : `0${digits}`
}
