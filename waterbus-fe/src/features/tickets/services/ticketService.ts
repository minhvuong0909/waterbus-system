import { withDelay } from '../../../mocks/delay'
import { ApiError } from '../../../services/apiClient'
import { buildAllTickets, buildManageBooking, buildTicketWallet } from '../../../mocks/tickets'
import { getTripDetail } from '../../trips/services/tripService'
import { buildBookedTickets } from '../utils'
import type { BookedSnapshot, ManageBooking, Ticket, TicketWallet } from '../types'

// Mock service layer: swap for real API calls later without touching the UI.
export function getTicketWallet(): Promise<TicketWallet> {
  return withDelay(buildTicketWallet())
}

/** Falls back to the next departure so the detail page never errors. */
export function getTicket(id: string | null): Promise<Ticket> {
  return withDelay(buildAllTickets().find((t) => t.id === id) ?? buildTicketWallet().next)
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
  return withDelay(buildManageBooking())
}

/** Pretends to submit a refund request. Always accepted so the flow never gets stuck. */
/** Rebuilds the wallet tickets (one per seat) for this session's bookings in the active language. */
export async function getBookedTickets(bookings: BookedSnapshot[]): Promise<Ticket[]> {
  const perBooking = await Promise.all(
    bookings.map(async (b) => {
      const trip = await getTripDetail(b.selectedTrip, b.tripId, b.query)
      return buildBookedTickets({ trip, seats: b.seats, fares: b.fares, passenger: b.passenger })
    }),
  )
  return perBooking.flat()
}
