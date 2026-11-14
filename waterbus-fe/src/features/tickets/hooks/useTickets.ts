import { useLocation } from 'react-router-dom'
import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { getAccountOrders, getSharedTrip } from '../services/orderAccessService'
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

/**
 * Booking-management data: the booking behind a secure link `token`, an order of the signed-in
 * account (`?orderId=`) or the order of this tab.
 */
export function useManageBooking(token?: string) {
  const { search } = useLocation()
  const fetched = useFetch(() => getManageBooking(token), [token, search])
  return { manage: fetched.data, loading: fetched.loading, error: fetched.error, retry: fetched.retry }
}

/** One page of the signed-in Passenger's orders; the previous page stays visible while the next loads. */
export function useAccountOrders(pageNumber: number) {
  const fetched = useFetch(() => getAccountOrders(pageNumber), [pageNumber])
  return {
    page: fetched.data,
    loading: fetched.loading,
    fetching: fetched.fetching,
    error: fetched.error,
    retry: fetched.retry,
  }
}

/** The journey behind a Passenger TripAccess Link. */
export function useSharedTrip(token: string) {
  const fetched = useFetch(() => getSharedTrip(token), [token])
  return { trip: fetched.data, loading: fetched.loading, error: fetched.error, retry: fetched.retry }
}
