import { withDelayExact } from '../../../mocks/delay'
import { ApiError } from '../../../services/apiClient'
import { buildAllTickets, buildManageBooking, buildTicketWallet } from '../../../mocks/tickets'
import { getTripDetail } from '../../trips/services/tripService'
import { buildBookedTickets, ticketStatusLabel } from '../utils'
import type { BookedSnapshot, ManageBooking, Ticket, TicketWallet } from '../types'
import { USE_MOCK } from '../../../constant/env'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { loadBooking } from '../../booking/bookingStorage'
import { getCheckout, getBookingQr } from '../../booking/services/bookingService'
import type { TripDetailDto } from '../../booking/types'
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
      id: ticket.ticketId, ticketCode: ticket.ticketCode, qrPayload: ticket.status === 'Valid' ? qr?.payload : undefined,
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

// Mock service layer: swap for real API calls later without touching the UI.
export async function getTicketWallet(): Promise<TicketWallet> {
  if (!USE_MOCK) {
    const tickets = await liveTickets()
    const upcoming = tickets.filter((t) => !t.completed)
    return { next: upcoming[0], later: upcoming.slice(1), past: tickets.filter((t) => t.completed) }
  }
  return withDelayExact(buildTicketWallet())
}

/** Falls back to the next departure so the detail page never errors. */
export async function getTicket(id: string | null): Promise<Ticket> {
  if (!USE_MOCK) {
    const tickets = await liveTickets()
    const ticket = id ? tickets.find((t) => t.id === id) : tickets[0]
    if (!ticket) throw new ApiError(404, 'Chưa có vé đã phát cho đơn hàng trong phiên này.')
    return ticket
  }
  return withDelayExact(buildAllTickets().find((t) => t.id === id) ?? buildTicketWallet().next)
}

// A secure link token is a long random string (the shape the backend's magic links will have).
const LINK_TOKEN = /^[A-Za-z0-9_-]{16,}$/

/**
 * Mock: the backend has no booking lookup or magic link yet (roadmap P1-BE-05 / P4-BE-03).
 * Without a `token` this is the signed-in traveler's booking; with one it is the booking behind
 * the link, or ApiError(404) when the link is not valid.
 */
export async function getManageBooking(token?: string): Promise<ManageBooking> {
  if (!USE_MOCK) {
    const tickets = await liveTickets(token)
    if (!tickets[0]) throw new ApiError(404, 'Mở đơn đã thanh toán trong phiên này hoặc cung cấp orderId và link bảo mật.')
    return { bookingCode: tickets[0].bookingRef, ticket: tickets[0] }
  }
  if (token !== undefined && !LINK_TOKEN.test(token)) {
    return Promise.reject(new ApiError(404, 'This link is invalid or has expired.'))
  }
  return withDelayExact(buildManageBooking())
}

/** Rebuilds the wallet tickets (one per seat) for this session's bookings in the active language. */
export async function getBookedTickets(bookings: BookedSnapshot[]): Promise<Ticket[]> {
  if (!USE_MOCK) return []
  const perBooking = await Promise.all(
    bookings.map(async (b) => {
      const trip = await getTripDetail(b.selectedTrip, b.tripId, b.query)
      return buildBookedTickets({ trip, seats: b.seats, fares: b.fares, passengerNames: b.passengerNames })
    }),
  )
  return perBooking.flat()
}
