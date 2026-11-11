import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { getManageBooking, getTicket, getTicketWallet } from '../services/ticketService'
import { selectTicket } from '../ticketsSlice'

/** The wallet: the tickets the backend issued for the order in this session or link. */
export function useTicketWallet() {
  const dispatch = useAppDispatch()
  const fetched = useFetch(getTicketWallet)

  return {
    wallet: fetched.data,
    loading: fetched.loading,
    error: fetched.error,
    retry: fetched.retry,
    openTicket: (id: string) => dispatch(selectTicket(id)),
  }
}

export function useTicketDetail() {
  const id = useAppSelector((s) => s.tickets.selectedTicketId)
  const fetched = useFetch(() => getTicket(id))
  return { ticket: fetched.data, loading: fetched.loading, error: fetched.error, retry: fetched.retry }
}

/** Booking-management data: opened with a secure link `token` it is the booking behind that link. */
export function useManageBooking(token?: string) {
  const fetched = useFetch(() => getManageBooking(token), [token])
  return { manage: fetched.data, loading: fetched.loading, error: fetched.error, retry: fetched.retry }
}
