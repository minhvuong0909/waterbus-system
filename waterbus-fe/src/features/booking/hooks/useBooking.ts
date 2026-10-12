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
import type { PaymentMethodId } from '../../payment/types'
import type { PassengerForm, Voucher } from '../types'
import { computeTotals } from '../utils'

/** Everything a booking page needs: trip, seat, passenger, voucher and totals. */
export function useBooking() {
  const dispatch = useAppDispatch()
  const booking = useAppSelector((s) => s.booking)
  const { detail, loading: tripLoading, error: tripError, retry: retryTrip } = useTripDetail()
  const seatMap = useFetch(getSeatMap)

  const seat = useMemo(
    () => seatMap.data?.seats.find((s) => s.id === booking.seatId),
    [seatMap.data, booking.seatId],
  )
  const totals = useMemo(
    () => (detail && seatMap.data ? computeTotals(detail, seat, booking.voucher) : undefined),
    [detail, seatMap.data, seat, booking.voucher],
  )

  return {
    loading: tripLoading || seatMap.loading || !detail || !seatMap.data,
    error: tripError ?? seatMap.error,
    retry: () => {
      if (tripError) retryTrip()
      if (seatMap.error) seatMap.retry()
    },
    trip: detail,
    seatMap: seatMap.data,
    seat,
    seatId: booking.seatId,
    passenger: booking.passenger,
    voucher: booking.voucher,
    termsAccepted: booking.termsAccepted,
    paymentMethod: booking.paymentMethod,
    totals,
    /** Occupied and unknown seats are ignored, so the booking never holds a seat nobody can take. */
    chooseSeat: (id: string) => {
      const target = seatMap.data?.seats.find((s) => s.id === id)
      if (target?.status === 'available') dispatch(selectSeat(id))
    },
    editPassenger: (patch: Partial<PassengerForm>) => dispatch(updatePassenger(patch)),
    setVoucher: (v: Voucher | null) => dispatch(applyVoucher(v)),
    setTerms: (v: boolean) => dispatch(setTermsAccepted(v)),
    setMethod: (m: PaymentMethodId) => dispatch(setPaymentMethod(m)),
  }
}
