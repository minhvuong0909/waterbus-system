import { t } from '../../i18n'
import { bookingReference, seatPosition, seatZone } from '../booking/utils'
import type { Seat } from '../booking/types'
import type { TripDetail } from '../trips/types'
import { toIsoDate } from '../../utils/time'
import type { BookedSnapshot, Ticket } from './types'

/** Ticket code in the backend's format ("TK" and 8 digits), derived from the trip and seat. */
export function ticketCodeOf(tripId: string, seatId: string): string {
  let hash = 0
  for (const char of `${tripId}${seatId}`) hash = (Math.imul(hash, 31) + char.charCodeAt(0)) >>> 0
  return `TK${String(hash % 100_000_000).padStart(8, '0')}`
}

/** True when the ticket is for today. */
export const isTicketToday = (ticket: Pick<Ticket, 'dateId'>): boolean =>
  ticket.dateId === toIsoDate(new Date())

/** Stable id of the ticket for a trip and seat. */
export function bookedTicketId(tripId: string, seatId: string): string {
  return `tkt-${tripId}-${seatId.toLowerCase()}`
}

/** Id of a booking made in this session: the trip and its seats. */
export function bookingSnapshotId(tripId: string, seats: Seat[]): string {
  return `bk-${tripId}-${seats.map((s) => s.id.toLowerCase()).join('-')}`
}

/** Ids of the tickets a booking issues, one per seat. */
export function snapshotTicketIds(booking: BookedSnapshot): string[] {
  return booking.seats.map((seat) => bookedTicketId(booking.tripId, seat.id))
}

interface BuildTicketsInput {
  trip: TripDetail
  seats: Seat[]
  fares: number[]
  /** Name of the passenger on each seat, in the order of `seats`. */
  passengerNames: string[]
}

/**
 * Turns the finished booking into wallet tickets, one per seat, all under the same booking
 * reference (mock: the backend will issue the real ones).
 */
export function buildBookedTickets({ trip, seats, fares, passengerNames }: BuildTicketsInput): Ticket[] {
  const bookingRef = bookingReference(seats)
  return seats.map((seat, index) => ({
    id: bookedTicketId(trip.id, seat.id),
    tripCode: trip.vesselCode,
    lineName: t('Smart Waterbus • {line}', { line: trip.lineLabel }),
    routeTag: t('Main Route'),
    bookingRef,
    ticketCode: ticketCodeOf(trip.id, seat.id),
    status: t('Confirmed'),
    completed: false,
    departTime: trip.departTime,
    departPier: trip.originPierName,
    departShort: trip.originShortName,
    departDistrict: trip.originDistrict,
    arriveTime: trip.arriveTime,
    arrivePier: trip.destinationPierName,
    arriveShort: trip.destinationShortName,
    arriveDistrict: trip.destinationDistrict,
    durationMins: trip.durationMins,
    dateLabel: trip.dateFull,
    dateId: trip.dateId,
    dateShort: trip.dateShort,
    passenger: passengerNames[index] ?? '',
    seat: t('Seat {id}', { id: seat.code }),
    seatNote: `${seatPosition(seat)} • ${seatZone(seat)}`,
    windowSeat: seat.window,
    fareVnd: fares[index] ?? trip.fareVnd,
    vesselNote: `${trip.vesselName} ${trip.vesselCode}`,
    gate: trip.originShortName,
  }))
}

/** Gom các vé cùng một đặt chỗ (cùng `bookingRef`), giữ thứ tự xuất hiện đầu tiên. */
export function groupByBooking(tickets: Ticket[]): Ticket[][] {
  const groups = new Map<string, Ticket[]>()
  for (const ticket of tickets) {
    const group = groups.get(ticket.bookingRef)
    if (group) group.push(ticket)
    else groups.set(ticket.bookingRef, [ticket])
  }
  return [...groups.values()]
}
