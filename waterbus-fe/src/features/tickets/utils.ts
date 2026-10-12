import { t } from '../../i18n'
import { bookingReference, seatPosition, seatZone } from '../booking/utils'
import type { PassengerForm, Seat } from '../booking/types'
import type { TripDetail } from '../trips/types'
import type { Ticket } from './types'

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
    routeTag: t('Central Route'),
    bookingRef: bookingReference(seatId),
    ticketCode: `TKT-${trip.vesselCode.replace('-', '')}-0842-${seatId}`,
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
    dateShort: trip.dateShort,
    passenger: passenger.fullName,
    seat: t('Seat {id}', { id: seatId }),
    seatNote: `${seatPosition(seat)} • ${seatZone(seat)}`,
    windowSeat: Boolean(seat?.window),
    fareVnd: totalVnd,
    vesselNote: `${trip.vesselName} ${trip.vesselCode}`,
    gate: trip.originGate,
  }
}
