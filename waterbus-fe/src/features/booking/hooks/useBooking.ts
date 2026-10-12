import { useMemo } from 'react'
import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { useTripDetail } from '../../trips/hooks/useTripSearch'
import {
  applyVoucher,
  selectSeat,
  setPaymentMethod,
  setTermsAccepted,
  updatePassenger,
} from '../bookingSlice'
import { getSeatMap } from '../services/bookingService'
import { useLiveSeatMap } from './useLiveSeatMap'
import type { PaymentMethodId } from '../../payment/types'
import type { PassengerForm, Voucher } from '../types'
import { computeTotals } from '../utils'

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

  const seat = useMemo(
    () => seatMap?.seats.find((s) => s.id === booking.seatId),
    [seatMap, booking.seatId],
  )
  const totals = useMemo(
    () => (detail && seatMap ? computeTotals(detail, seat, booking.voucher) : undefined),
    [detail, seatMap, seat, booking.voucher],
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
    seat,
    seatId: booking.seatId,
    passenger: booking.passenger,
    voucher: booking.voucher,
    termsAccepted: booking.termsAccepted,
    paymentMethod: booking.paymentMethod,
    totals,
    /** Sold, held and unknown seats are ignored, so the booking never targets a seat nobody can take. */
    chooseSeat: (id: string) => {
      const target = seatMap?.seats.find((s) => s.id === id)
      if (target?.status === 'available') dispatch(selectSeat(id))
    },
    editPassenger: (patch: Partial<PassengerForm>) => dispatch(updatePassenger(patch)),
    setVoucher: (v: Voucher | null) => dispatch(applyVoucher(v)),
    setTerms: (v: boolean) => dispatch(setTermsAccepted(v)),
    setMethod: (m: PaymentMethodId) => dispatch(setPaymentMethod(m)),
  }
}
