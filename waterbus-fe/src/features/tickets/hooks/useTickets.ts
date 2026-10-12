import { combineFetches, useFetch } from '../../../hooks'
import type { FetchState } from '../../../hooks/useFetch'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import {
  getBookedTickets,
  getManageBooking,
  getTicket,
  getTicketWallet,
} from '../services/ticketService'
import { selectTicket } from '../ticketsSlice'
import type { ManageBooking, Ticket, TicketWallet } from '../types'

/** Tickets for the bookings made in this session, newest first. */
function useBookedTickets(): FetchState<Ticket[]> {
  const bookings = useAppSelector((s) => s.tickets.booked)
  return useFetch(() => getBookedTickets(bookings))
}

/** The wallet, with the newest booking (if any) shown as the next departure. */
export function useTicketWallet() {
  const dispatch = useAppDispatch()
  const fetched = useFetch(getTicketWallet)
  const booked = useBookedTickets()

  const bookedTickets = booked.data ?? []
  // Newest booking is the next departure; older ones and the sample trips follow.
  const wallet: TicketWallet | undefined =
    fetched.data && bookedTickets.length > 0
      ? {
          ...fetched.data,
          next: bookedTickets[0],
          later: [...bookedTickets.slice(1), fetched.data.next, ...fetched.data.later],
        }
      : fetched.data

  return {
    wallet,
    ...combineFetches(fetched, booked),
    openTicket: (id: string) => dispatch(selectTicket(id)),
  }
}

export function useTicketDetail() {
  const id = useAppSelector((s) => s.tickets.selectedTicketId)
  const isBooked = useAppSelector((s) => s.tickets.booked.some((b) => b.id === id))
  const fetched = useFetch(() => getTicket(id))
  const booked = useBookedTickets()

  const source = isBooked ? booked : fetched
  return {
    ticket: isBooked ? booked.data?.find((t) => t.id === id) : fetched.data,
    loading: source.loading,
    error: source.error,
    retry: source.retry,
  }
}

/** Booking-management data, pointed at the newest booking when there is one. */
export function useManageBooking() {
  const fetched = useFetch(getManageBooking)
  const booked = useBookedTickets()

  const newest = booked.data?.[0]
  const manage: ManageBooking | undefined =
    fetched.data && newest
      ? { ...fetched.data, bookingCode: newest.bookingRef, ticket: newest }
      : fetched.data

  return { manage, ...combineFetches(fetched, booked) }
}
