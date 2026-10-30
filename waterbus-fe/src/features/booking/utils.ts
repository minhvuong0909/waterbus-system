import { t } from '../../i18n'
import type { TripDetail } from '../trips/types'
import type { CreateHoldRequest, Journey, SeatMap, SubmitCheckoutRequest, PurchaserForm, Seat, SeatCategory, SeatPassengerForm } from './types'
import type { TripDetailDto, TripSeatDto, TripSeatMapDto } from '../trips/types'

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

export interface BookingTotals {
  seatCount: number
  /** Sum of the seat fares; the backend charges nothing else. */
  totalVnd: number
}

/** The totals of both legs of a round trip, or the first ones alone. */
export function addTotals(a: BookingTotals, b?: BookingTotals): BookingTotals {
  if (!b) return a
  return {
    seatCount: a.seatCount + b.seatCount,
    totalVnd: a.totalVnd + b.totalVnd,
  }
}

/**
 * What the chosen seats cost. Each seat carries its own fare from the backend (flat per trip type and
 * seat class, BR-FARE); the trip's lowest fare stands in only while a seat has none.
 */
export function computeTotals(trip: TripDetail, seats: Seat[]): BookingTotals {
  const totalVnd = seats.reduce((sum, seat) => sum + (seat.priceVnd ?? trip.fareVnd), 0)
  return { seatCount: seats.length, totalVnd }
}

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
  /** The return leg of a round trip, whose seats need passengers too. */
  returnLeg?: { seatIds: string[]; passengers: Record<string, SeatPassengerForm> },
): boolean {
  const named = (ids: string[], people: Record<string, SeatPassengerForm>) =>
    ids.length > 0 && ids.every((id) => Object.keys(seatPassengerErrors(people[id])).length === 0)
  return (
    Object.keys(purchaserErrors(purchaser)).length === 0 &&
    named(seatIds, passengers) &&
    (!returnLeg || named(returnLeg.seatIds, returnLeg.passengers))
  )
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
export const isGuid = (value: string): boolean => GUID.test(value)

export interface HoldInput {
  tripId: string
  originId: string
  destinationId: string
  seatIds: string[]
  /** Real stops of the trip, when the seat map came from the backend. */
  journey?: Journey
}

/**
 * The `POST /checkouts/holds` body, naming the real stops when they are known: one leg for a
 * one-way booking, two (outbound, then return) for a round trip, held together (BR-HOLD-02).
 */
export function buildHoldRequest(input: HoldInput | HoldInput[]): CreateHoldRequest {
  const legs = Array.isArray(input) ? input : [input]
  return {
    legs: legs.map(({ tripId, originId, destinationId, seatIds, journey }) => ({
      tripId,
      seatIds,
      boardingStationId: originId,
      disembarkingStationId: destinationId,
      boardingCallId: journey?.boardingCallId,
      disembarkingCallId: journey?.disembarkingCallId,
    })),
  }
}

/** One booking of the order and the passengers of its seats. */
export interface SubmitBooking {
  bookingId: string
  seatIds: string[]
  passengers: Record<string, SeatPassengerForm>
}

type SubmitInput =
  | (SubmitBooking & { purchaser: PurchaserForm })
  | { purchaser: PurchaserForm; bookings: SubmitBooking[] }

/**
 * The submit body: the buyer, and the passenger of every held seat of every booking
 * (BR-COM-06/07). A round trip names the seats of both bookings, each under its own booking.
 */
export function buildSubmitRequest(input: SubmitInput): SubmitCheckoutRequest {
  const { purchaser } = input
  const bookings = 'bookings' in input ? input.bookings : [input]
  return {
    customerName: purchaser.fullName.trim(),
    customerEmail: purchaser.email.trim(),
    customerPhone: toCustomerPhone(purchaser),
    passengers: bookings.flatMap(({ bookingId, seatIds, passengers }) =>
      seatIds.map((seatId) => ({
        bookingId,
        seatId,
        name: passengers[seatId]?.fullName.trim() ?? '',
        email: passengers[seatId]?.email.trim() || undefined,
        phone: passengers[seatId]?.phone.trim() || undefined,
      })),
    ),
  }
}


const COLUMN_LETTERS = ['A', 'B', 'C', 'D'] as const

/**
 * Cabin of a seat from its class name. The seeded classes are Vietnamese ("Khoang trước VIP",
 * "Boong ngoài trời", "Tiêu chuẩn"); English names are kept for older data. Accents are dropped
 * so "truoc" and "trước" match alike.
 */
function seatCategoryOf(className: string | null): SeatCategory {
  const name = (className ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/đ/g, 'd')
    .toLowerCase()
  if (name.includes('front') || name.includes('khoang truoc') || name.includes('vip')) return 'front'
  if (name.includes('outdoor') || name.includes('deck') || name.includes('boong') || name.includes('ngoai troi'))
    return 'outdoor'
  return 'standard'
}

function toSeat(dto: TripSeatDto): Seat {
  const column = COLUMN_LETTERS[Math.min(Math.max(dto.columnNumber, 1), 4) - 1]
  const status: Seat['status'] =
    dto.status === 'available' ? 'available' : dto.status === 'held' ? 'held' : 'sold'
  return {
    id: dto.seatId,
    code: dto.seatCode,
    category: seatCategoryOf(dto.seatClassName),
    row: dto.rowNumber,
    column,
    // A seat without a fare (or otherwise unavailable) cannot be chosen, so it is shown as taken.
    status: dto.price === null ? 'sold' : status,
    window: column === 'A' || column === 'D',
    priceVnd: dto.price ?? undefined,
  }
}

/**
 * The backend reports the seats of the traveler's own hold as `held`, like anyone else's. While
 * that hold runs, those seats are the traveler's: shown as free so they stay selectable (and can be
 * removed) when the traveler comes back to the seat page. Other travelers' holds are unchanged.
 */
export function withOwnHold(
  seatMap: SeatMap,
  order: {
    seatIds?: string[]
    heldUntil?: number
    settled?: boolean
    tripId?: string | null
    /** Seats held per trip (two legs for a round trip). */
    legs?: { tripId: string | null; seatIds: string[] }[]
  } | null,
  tripId: string | null,
  now = Date.now(),
): SeatMap {
  if (!order || order.settled || !order.heldUntil || order.heldUntil <= now || !tripId) return seatMap
  const legs = order.legs ?? [{ tripId: order.tripId ?? null, seatIds: order.seatIds ?? [] }]
  // Only the seats held on this trip: the same boat seat has the same id on each of its trips.
  const own = new Set(legs.filter((l) => l.tripId === tripId).flatMap((l) => l.seatIds))
  if (own.size === 0) return seatMap
  if (!seatMap.seats.some((seat) => seat.status === 'held' && own.has(seat.id))) return seatMap
  return {
    ...seatMap,
    seats: seatMap.seats.map((seat) =>
      seat.status === 'held' && own.has(seat.id) ? { ...seat, status: 'available' } : seat,
    ),
  }
}

/** Maps the backend seat map of one journey segment to the seat map the pages use. */
export function toSeatMap(dto: TripSeatMapDto, detail: Pick<TripDetailDto, 'boat'>): SeatMap {
  return {
    vesselName: detail.boat?.name ?? '',
    seats: dto.seats.map(toSeat),
    canBook: dto.canBook,
    journey: {
      boardingCallId: dto.boardingCallId,
      disembarkingCallId: dto.disembarkingCallId,
      boardingVisitOrder: dto.boardingVisitOrder,
      disembarkingVisitOrder: dto.disembarkingVisitOrder,
    },
  }
}
