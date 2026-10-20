import { useMemo } from 'react'
import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { useTripDetail } from '../../trips/hooks/useTripSearch'
import {
  toggleSeat,
  setPaymentMethod,
  setTermsAccepted,
  updatePurchaser,
  updateSeatPassenger,
} from '../bookingSlice'
import { getSeatMap } from '../services/bookingService'
import { useLiveSeatMap } from './useLiveSeatMap'
import type { PaymentMethodId } from '../../payment/types'
import type { PurchaserForm, SeatPassengerForm } from '../types'
import { MAX_SEATS_PER_BOOKING, computeTotals, withOwnHold } from '../utils'

interface UseBookingOptions {
  /** Apply seat holds from other travelers as they happen (only the seat page needs this). */
  live?: boolean
}

/** Everything a booking page needs: trip, seat, passenger and totals. */
export function useBooking({ live = false }: UseBookingOptions = {}) {
  const dispatch = useAppDispatch()
  const booking = useAppSelector((s) => s.booking)
  const { detail, loading: tripLoading, error: tripError, retry: retryTrip } = useTripDetail()
  const loadedSeatMap = useFetch(
    () => getSeatMap(booking.selectedTripId, booking.query.originId, booking.query.destinationId),
    [booking.selectedTripId, booking.query.originId, booking.query.destinationId],
  )
  const liveSeatMap = useLiveSeatMap(loadedSeatMap.data, detail?.id ?? null, live, loadedSeatMap.retry)
  // Seats of the traveler's own running hold are theirs, not "held by another traveler".
  const seatMap = useMemo(
    () => (liveSeatMap ? withOwnHold(liveSeatMap, booking.order) : undefined),
    [liveSeatMap, booking.order],
  )

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
    () => (detail && seatMap ? computeTotals(detail, seats) : undefined),
    [detail, seatMap, seats],
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
    purchaser: booking.purchaser,
    /** The passenger of each chosen seat, by seat id. */
    passengers: booking.passengers,
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
    editPurchaser: (patch: Partial<PurchaserForm>) => dispatch(updatePurchaser(patch)),
    editSeatPassenger: (seatId: string, patch: Partial<SeatPassengerForm>) =>
      dispatch(updateSeatPassenger({ seatId, patch })),
    setTerms: (v: boolean) => dispatch(setTermsAccepted(v)),
    setMethod: (m: PaymentMethodId) => dispatch(setPaymentMethod(m)),
  }
}
