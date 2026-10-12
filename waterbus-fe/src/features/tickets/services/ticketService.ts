import { withDelay } from '../../../mocks/delay'
import { ApiError } from '../../../services/apiClient'
import { ALL_TICKETS, MANAGE_BOOKING, TICKET_WALLET } from '../../../mocks/tickets'
import { getTripDetail } from '../../trips/services/tripService'
import { buildBookedTicket } from '../utils'
import type { BookedSnapshot, ManageBooking, Ticket, TicketWallet } from '../types'

// Mock service layer: swap for real API calls later without touching the UI.
export function getTicketWallet(): Promise<TicketWallet> {
  return withDelay(TICKET_WALLET)
}

/** Falls back to the next departure so the detail page never errors. */
export function getTicket(id: string | null): Promise<Ticket> {
  return withDelay(ALL_TICKETS.find((t) => t.id === id) ?? TICKET_WALLET.next)
}

// A secure link token is a long random string (the shape the backend's magic links will have).
const LINK_TOKEN = /^[A-Za-z0-9_-]{16,}$/

/**
 * Mock: the backend has no booking lookup or magic link yet (roadmap P1-BE-05 / P4-BE-03).
 * Without a `token` this is the signed-in traveler's booking; with one it is the booking behind
 * the link, or ApiError(404) when the link is not valid.
 */
export function getManageBooking(token?: string): Promise<ManageBooking> {
  if (token !== undefined && !LINK_TOKEN.test(token)) {
    return Promise.reject(new ApiError(404, 'This link is invalid or has expired.'))
  }
  return withDelay(MANAGE_BOOKING)
}

/** Pretends to submit a refund request. Always accepted so the flow never gets stuck. */
export function requestRefund(
  bookingCode: string,
): Promise<{ bookingCode: string; status: 'submitted' }> {
  return withDelay({ bookingCode, status: 'submitted' as const }, 500)
}

/** Rebuilds the wallet tickets for this session's bookings in the active language. */
export function getBookedTickets(bookings: BookedSnapshot[]): Promise<Ticket[]> {
  return Promise.all(
    bookings.map(async (b) => {
      const trip = await getTripDetail(b.selectedTrip, b.tripId, b.query)
      return buildBookedTicket({
        trip,
        seat: b.seat,
        seatId: b.seatId,
        passenger: b.passenger,
        totalVnd: b.totalVnd,
      })
    }),
  )
}
