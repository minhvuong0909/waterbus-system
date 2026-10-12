import { t } from '../../i18n'
import { bookingReference, seatCode, seatPosition, seatZone } from '../booking/utils'
import type { PassengerForm, Seat } from '../booking/types'
import type { TripDetail } from '../trips/types'
import { toIsoDate } from '../../utils/time'
import type { Ticket } from './types'

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

interface BuildTicketInput {
  trip: TripDetail
  seat: Seat | undefined
  seatId: string
  passenger: PassengerForm
  totalVnd: number
}

/** Turns the finished booking into a wallet ticket (mock: the backend will issue the real one). */
export function buildBookedTicket({
  trip,
  seat,
  seatId,
  passenger,
  totalVnd,
}: BuildTicketInput): Ticket {
  return {
    id: bookedTicketId(trip.id, seatId),
    tripCode: trip.vesselCode,
    lineName: t('Smart Waterbus • {line}', { line: trip.lineLabel }),
    routeTag: t('Main Route'),
    bookingRef: bookingReference(seatId),
    ticketCode: ticketCodeOf(trip.id, seatId),
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
    passenger: passenger.fullName,
    seat: t('Seat {id}', { id: seatCode(seat, seatId) }),
    seatNote: `${seatPosition(seat)} • ${seatZone(seat)}`,
    windowSeat: Boolean(seat?.window),
    fareVnd: totalVnd,
    vesselNote: `${trip.vesselName} ${trip.vesselCode}`,
    gate: trip.originShortName,
  }
}
