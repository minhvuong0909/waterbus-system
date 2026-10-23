import { useMemo } from 'react'
import { useFetch } from '../../../hooks'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { getTripDetail } from '../../trips/services/tripService'
import {
  toggleReturnSeat,
  toggleSeat,
  setTermsAccepted,
  updatePurchaser,
  updateReturnPassenger,
  updateSeatPassenger,
} from '../bookingSlice'
import { legView } from '../legs'
import type { Leg } from '../legs'
import { getSeatMap } from '../services/bookingService'
import { useLiveSeatMap } from './useLiveSeatMap'
import type { TripDetail } from '../../trips/types'
import type { PurchaserForm, SeatMap, SeatPassengerForm } from '../types'
import { MAX_SEATS_PER_BOOKING, computeTotals, withOwnHold } from '../utils'

interface UseBookingOptions {
  /** Apply seat holds from other travelers as they happen (only the seat page needs this). */
  live?: boolean
  /** Which leg: the outbound one (default) or the return leg of a round trip. */
  leg?: Leg
  /** False skips every request, e.g. for the return leg of a one-way booking. */
  enabled?: boolean
}

const nothing = <T,>(): Promise<T | undefined> => Promise.resolve(undefined)

/** Everything a booking page needs for one leg: trip, seats, passengers and totals. */
export function useBooking({ live = false, leg = 'outbound', enabled = true }: UseBookingOptions = {}) {
  const dispatch = useAppDispatch()
  const booking = useAppSelector((s) => s.booking)
  const view = useMemo(() => legView(booking, leg), [booking, leg])
  const { originId, destinationId } = view.query

  const tripDetail = useFetch<TripDetail | undefined>(
    () => (enabled ? getTripDetail(view.trip, view.tripId, view.query) : nothing<TripDetail>()),
    [enabled, leg, view.tripId],
  )
  const loadedSeatMap = useFetch<SeatMap | undefined>(
    () => (enabled ? getSeatMap(view.tripId, originId, destinationId) : nothing<SeatMap>()),
    [enabled, view.tripId, originId, destinationId],
  )
  const detail = tripDetail.data
  const liveSeatMap = useLiveSeatMap(loadedSeatMap.data, detail?.id ?? null, live, loadedSeatMap.retry)
  // Seats of the traveler's own running hold are theirs, not "held by another traveler".
  const seatMap = useMemo(
    () => (liveSeatMap ? withOwnHold(liveSeatMap, booking.order, detail?.id ?? null) : undefined),
    [liveSeatMap, booking.order, detail?.id],
  )

  // The chosen seats as they are on the map now; an id the map does not know is left out.
  const seats = useMemo(
    () =>
      view.seatIds.flatMap((id) => {
        const found = seatMap?.seats.find((s) => s.id === id)
        return found ? [found] : []
      }),
    [seatMap, view.seatIds],
  )
  const totals = useMemo(
    () => (detail && seatMap ? computeTotals(detail, seats) : undefined),
    [detail, seatMap, seats],
  )

  return {
    leg,
    /** No trip chosen in this session (e.g. a booking page opened directly): nothing to load. */
    noTrip: enabled && !view.tripId,
    loading: enabled && (tripDetail.loading || loadedSeatMap.loading || !detail || !seatMap),
    error: tripDetail.error ?? loadedSeatMap.error,
    retry: () => {
      if (tripDetail.error) tripDetail.retry()
      if (loadedSeatMap.error) loadedSeatMap.retry()
    },
    trip: detail,
    seatMap,
    seats,
    seatIds: view.seatIds,
    /** True when no more seats can be added to this booking. */
    seatLimitReached: view.seatIds.length >= MAX_SEATS_PER_BOOKING,
    purchaser: booking.purchaser,
    /** The passenger of each chosen seat of this leg, by seat id. */
    passengers: view.passengers,
    termsAccepted: booking.termsAccepted,
    totals,
    /**
     * Adds or removes a seat. Sold, held and unknown seats cannot be added, so the booking never
     * targets a seat nobody can take; a chosen seat can always be removed.
     */
    toggleSeat: (id: string) => {
      const target = seatMap?.seats.find((s) => s.id === id)
      if (target && (target.status === 'available' || view.seatIds.includes(id))) {
        dispatch(leg === 'outbound' ? toggleSeat(id) : toggleReturnSeat(id))
      }
    },
    editPurchaser: (patch: Partial<PurchaserForm>) => dispatch(updatePurchaser(patch)),
    editSeatPassenger: (seatId: string, patch: Partial<SeatPassengerForm>) =>
      dispatch(leg === 'outbound' ? updateSeatPassenger({ seatId, patch }) : updateReturnPassenger({ seatId, patch })),
    setTerms: (v: boolean) => dispatch(setTermsAccepted(v)),
  }
}
