import { useMemo } from 'react'
import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { useTripDetail } from '../../trips/hooks/useTripSearch'
import {
  applyVoucher,
  toggleSeat,
  setPaymentMethod,
  setTermsAccepted,
  updatePassenger,
} from '../bookingSlice'
import { getSeatMap } from '../services/bookingService'
import { useLiveSeatMap } from './useLiveSeatMap'
import type { PaymentMethodId } from '../../payment/types'
import type { PassengerForm, Voucher } from '../types'
import { MAX_SEATS_PER_BOOKING, computeTotals } from '../utils'

interface UseBookingOptions {
  /** Apply seat holds from other travelers as they happen (only the seat page needs this). */
  live?: boolean
}

/** Everything a booking page needs: trip, seat, passenger, voucher and totals. */
export function useBooking({ live = false }: UseBookingOptions = {}) {
  const dispatch = useAppDispatch()
  const booking = useAppSelector((s) => s.booking)
  const { detail, loading: tripLoading, error: tripError, retry: retryTrip } = useTripDetail()
  const loadedSeatMap = useFetch(getSeatMap)
  const seatMap = useLiveSeatMap(loadedSeatMap.data, detail?.id ?? null, live)

  // The chosen seats as they are on the map now; an id the map does not know is left out.
  const seats = useMemo(
    () =>
      booking.seatIds.flatMap((id) => {
        const found = seatMap?.seats.find((s) => s.id === id)
        return found ? [found] : []
      }),
    [seatMap, booking.seatIds],
  )
  const totals = useMemo(
    () => (detail && seatMap ? computeTotals(detail, seats, booking.voucher) : undefined),
    [detail, seatMap, seats, booking.voucher],
  )

  return {
    loading: tripLoading || loadedSeatMap.loading || !detail || !seatMap,
    error: tripError ?? loadedSeatMap.error,
    retry: () => {
      if (tripError) retryTrip()
      if (loadedSeatMap.error) loadedSeatMap.retry()
    },
    trip: detail,
    seatMap,
    seats,
    seatIds: booking.seatIds,
    /** True when no more seats can be added to this booking. */
    seatLimitReached: booking.seatIds.length >= MAX_SEATS_PER_BOOKING,
    passenger: booking.passenger,
    voucher: booking.voucher,
    termsAccepted: booking.termsAccepted,
    paymentMethod: booking.paymentMethod,
    totals,
    /**
     * Adds or removes a seat. Sold, held and unknown seats cannot be added, so the booking never
     * targets a seat nobody can take; a chosen seat can always be removed.
     */
    toggleSeat: (id: string) => {
      const target = seatMap?.seats.find((s) => s.id === id)
      if (target && (target.status === 'available' || booking.seatIds.includes(id))) {
        dispatch(toggleSeat(id))
      }
    },
    editPassenger: (patch: Partial<PassengerForm>) => dispatch(updatePassenger(patch)),
    setVoucher: (v: Voucher | null) => dispatch(applyVoucher(v)),
    setTerms: (v: boolean) => dispatch(setTermsAccepted(v)),
    setMethod: (m: PaymentMethodId) => dispatch(setPaymentMethod(m)),
  }
}
