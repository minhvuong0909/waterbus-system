import { ApiError } from '../../../services/apiClient'
import { getTripDetail } from '../../trips/services/tripService'
import { ticketStatusLabel } from '../utils'
import type { ManageBooking, Ticket, TicketWallet } from '../types'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { loadBooking } from '../../booking/bookingStorage'
import { getCheckout, getBookingQr } from '../../booking/services/bookingService'
import type { TripDetailDto } from '../../trips/types'
import { toTrip } from '../../trips/utils'
import { toVietnamDate } from '../../../utils/time'

/** Chỉ hiện Ticket đã được backend phát; chưa có API lịch sử toàn bộ tài khoản. */
async function liveTickets(token?: string): Promise<Ticket[]> {
  const saved = loadBooking()
  const orderId = new URLSearchParams(window.location.search).get('orderId') ?? saved.order?.orderId
  if (!orderId) return []
  const accessToken = token ?? saved.order?.accessToken ?? null
  const order = await getCheckout(orderId, accessToken)
  const paid = order.bookings.filter((booking) => booking.tickets.length > 0)

  // One trip is read once, even when several bookings of the order ride it.
  const details = new Map<string, Promise<TripDetailDto>>()
  const detailOf = (tripId: string) => {
    if (!details.has(tripId)) details.set(tripId, apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId)))
    return details.get(tripId) as Promise<TripDetailDto>
  }

  const tickets = await Promise.all(paid.map(async (booking) => {
    const raw = await detailOf(booking.tripId)
    const boarding = raw.stops.find((s) => s.callId === booking.boardingCallId)
    const alighting = raw.stops.find((s) => s.callId === booking.disembarkingCallId)
    if (!boarding || !alighting) throw new ApiError(409, 'Không tìm thấy điểm dừng của vé.')
    const [trip, qr] = await Promise.all([
      getTripDetail(
        toTrip({ id: raw.tripId, routeName: raw.routeName,
          departureStationName: boarding.stationName, arrivalStationName: alighting.stationName,
          departureTime: raw.departureTime, arrivalTime: raw.arrivalTime,
          durationMinutes: Math.round((Date.parse(raw.arrivalTime) - Date.parse(raw.departureTime)) / 60000),
          tripType: raw.tripType, basePrice: 0, availableSeatsCount: 0 }),
        raw.tripId,
        { tripType: 'one-way', originId: boarding.stationId, destinationId: alighting.stationId,
          dateId: toVietnamDate(new Date(raw.departureTime)) },
      ),
      booking.status === 'Confirmed' ? getBookingQr(orderId, booking.bookingId, accessToken) : null,
    ])
    return booking.tickets.map((ticket): Ticket => ({
      id: ticket.ticketId, tripId: raw.tripId, ticketCode: ticket.ticketCode, qrPayload: ticket.status === 'Valid' ? qr?.payload : undefined,
      tripCode: raw.boat?.code ?? raw.tripId, lineName: raw.routeName, routeTag: raw.tripType,
      bookingRef: booking.bookingCode, status: ticketStatusLabel(ticket.status, ticket.boardingStatus),
      departTime: trip.departTime, arriveTime: trip.arriveTime, durationMins: trip.durationMins,
      departPier: boarding.stationName, departShort: boarding.stationName, departDistrict: '',
      arrivePier: alighting.stationName, arriveShort: alighting.stationName, arriveDistrict: '',
      dateId: trip.dateId, dateLabel: trip.dateFull, dateShort: trip.dateShort,
      passenger: ticket.passengerName, seat: ticket.seatCode, seatNote: '', windowSeat: false,
      completed: ['Completed', 'Terminated'].includes(raw.status), fareVnd: ticket.paidFare,
      vesselNote: raw.boat?.name ?? '', gate: boarding.stationName,
    }))
  }))
  return tickets.flat()
}

/** The tickets of the order in this session (or behind the secure link), split into upcoming and past. */
export async function getTicketWallet(): Promise<TicketWallet> {
  const tickets = await liveTickets()
  const upcoming = tickets.filter((t) => !t.completed)
  return { next: upcoming[0], later: upcoming.slice(1), past: tickets.filter((t) => t.completed) }
}

/** The ticket with this id; the first ticket of the order when `id` is not one of them. */
export async function getTicket(id: string | null): Promise<Ticket> {
  const tickets = await liveTickets()
  // The success page may select an id that is not a backend ticket id: show the first ticket then.
  const ticket = (id ? tickets.find((t) => t.id === id) : undefined) ?? tickets[0]
  if (!ticket) throw new ApiError(404, 'Chưa có vé đã phát cho đơn hàng trong phiên này.')
  return ticket
}

/**
 * The booking of the order in this session or, with a secure link `token`, the order behind the
 * link. The backend has no booking lookup by account yet (roadmap P1-BE-05 / P4-BE-03).
 */
export async function getManageBooking(token?: string): Promise<ManageBooking> {
  const tickets = await liveTickets(token)
  if (!tickets[0]) throw new ApiError(404, 'Mở đơn đã thanh toán trong phiên này hoặc cung cấp orderId và link bảo mật.')
  return { bookingCode: tickets[0].bookingRef, ticket: tickets[0] }
}
