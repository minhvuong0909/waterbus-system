import { t } from '../../i18n'
import type { TripDetail } from '../trips/types'
import type {
  CreateCheckoutRequest,
  PurchaserForm,
  Seat,
  SeatCategory,
  SeatPassengerForm,
} from './types'

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
  totalVnd: number
}

/** What each ticket costs: the trip fare plus that seat's cabin surcharge. */
export function ticketFares(fareVnd: number, seats: Seat[]): number[] {
  return seats.map((seat) => fareVnd + seatFeeFor(seat, fareVnd))
}

export function computeTotals(trip: TripDetail, seats: Seat[]): BookingTotals {
  const fareVnd = trip.fareVnd * seats.length
  const seatFeeVnd = seats.reduce((sum, seat) => sum + seatFeeFor(seat, trip.fareVnd), 0)
  return { seatCount: seats.length, fareVnd, seatFeeVnd, totalVnd: fareVnd + seatFeeVnd }
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

/** Purchaser phone as the backend expects it: digits only with a leading 0, or undefined when none. */
export function toCustomerPhone(purchaser: Pick<PurchaserForm, 'phone'>): string | undefined {
  const digits = purchaser.phone.replace(/\D/g, '')
  if (!digits) return undefined
  return digits.startsWith('0') ? digits : `0${digits}`
}

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const isEmail = (value: string) => EMAIL.test(value.trim())

/** BR-COM-07: the purchaser needs a name and an email; the phone is optional but must be valid. */
export function purchaserErrors(purchaser: PurchaserForm): Partial<Record<keyof PurchaserForm, string>> {
  const errors: Partial<Record<keyof PurchaserForm, string>> = {}
  if (!purchaser.fullName.trim()) errors.fullName = t('Please enter the buyer’s full name.')
  if (!isEmail(purchaser.email)) errors.email = t('Please enter a valid email address.')
  const phone = toCustomerPhone(purchaser)
  if (phone && !/^[0-9]{10,11}$/.test(phone)) errors.phone = t('Phone number must have 10 to 11 digits.')
  return errors
}

/** BR-COM-06: every seat needs a passenger name; email and phone are optional. */
export function seatPassengerErrors(
  passenger: SeatPassengerForm | undefined,
): Partial<Record<keyof SeatPassengerForm, string>> {
  const errors: Partial<Record<keyof SeatPassengerForm, string>> = {}
  if (!passenger?.fullName.trim()) errors.fullName = t('Please enter the passenger’s full name.')
  if (passenger?.email.trim() && !isEmail(passenger.email)) {
    errors.email = t('Please enter a valid email address.')
  }
  return errors
}

/** True when the purchaser and the passenger of every chosen seat are filled in correctly. */
export function isBookingComplete(
  purchaser: PurchaserForm,
  seatIds: string[],
  passengers: Record<string, SeatPassengerForm>,
): boolean {
  return (
    seatIds.length > 0 &&
    Object.keys(purchaserErrors(purchaser)).length === 0 &&
    seatIds.every((id) => Object.keys(seatPassengerErrors(passengers[id])).length === 0)
  )
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
export const isGuid = (value: string): boolean => GUID.test(value)

interface CheckoutInput {
  tripId: string
  originId: string
  destinationId: string
  seatIds: string[]
  purchaser: PurchaserForm
  passengers: Record<string, SeatPassengerForm>
}

/** The `POST /checkouts` body: one leg, with the passengers in the same order as the seats. */
export function buildCheckoutRequest(input: CheckoutInput): CreateCheckoutRequest {
  const { tripId, originId, destinationId, seatIds, purchaser, passengers } = input
  return {
    customerName: purchaser.fullName.trim(),
    customerEmail: purchaser.email.trim(),
    customerPhone: toCustomerPhone(purchaser),
    legs: [
      {
        tripId,
        seatIds,
        boardingStationId: originId,
        disembarkingStationId: destinationId,
        passengers: seatIds.map((id) => ({
          name: passengers[id]?.fullName.trim() ?? '',
          email: passengers[id]?.email.trim() || undefined,
          phone: passengers[id]?.phone.trim() || undefined,
        })),
      },
    ],
  }
}
