import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { ApiError } from '../../../services/apiClient'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { holdStarted, holdSubmitted, orderReleased, setJourney, startReturnLeg } from '../bookingSlice'
import { isRoundTrip } from '../legs'
import type { Leg } from '../legs'
import { cancelCheckout, createHold, submitCheckout } from '../services/bookingService'
import type { BookingHold, Journey } from '../types'
import { buildHoldRequest, buildSubmitRequest } from '../utils'
import type { HoldInput } from '../utils'

export interface BookingFailure {
  /** Message to show. Empty means the generic "try again" text. */
  message: string
  /** True when a seat was taken meanwhile, so the traveler has to pick another one. */
  seatTaken: boolean
}

function toFailure(error: unknown): BookingFailure {
  const known = error instanceof ApiError && error.status !== 0
  return { message: known ? error.message : '', seatTaken: known && error.status === 409 }
}

const sameSeats = (a: string[], b: string[]) =>
  a.length === b.length && a.every((id) => b.includes(id))

/** True while the hold covers exactly these seats on each leg, so it can be reused. */
function holdCovers(hold: BookingHold | null, legs: { seatIds: string[] }[]): boolean {
  if (!hold || hold.expiresAt <= Date.now()) return false
  const held = hold.bookings ?? [{ seatIds: hold.seatIds }]
  return held.length === legs.length && legs.every((leg, i) => sameSeats(held[i].seatIds, leg.seatIds))
}

/** Where the return trip is chosen, after the outbound seats of a round trip. */
export const RETURN_RESULTS = `${ROUTES.searchResults}?leg=return`

/**
 * Step 1, on the seat page: holds the chosen seats (`POST /checkouts/holds`) before the buyer is
 * asked for anything, then moves on to the passenger details. A hold that still runs for the same
 * seats is reused; a hold left over from other seats is released first.
 *
 * For a round trip both legs are held in one request (BR-HOLD-02): the outbound seat page only
 * keeps its stops and moves on to the return trip; the return seat page holds both legs.
 */
export function useSeatHold() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { seatIds, hold, order, query, selectedTripId, returnLeg, journeys } = useAppSelector((s) => s.booking)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<BookingFailure | null>(null)

  const submit = async (leg: Leg, tripId: string, journey: Journey | undefined) => {
    const roundTrip = isRoundTrip(query)
    if (roundTrip && leg === 'outbound') {
      if (journey) dispatch(setJourney({ leg: 'outbound', journey }))
      dispatch(startReturnLeg())
      navigate(RETURN_RESULTS)
      return
    }

    const outbound: HoldInput = {
      tripId: leg === 'outbound' ? tripId : (selectedTripId ?? ''),
      originId: query.originId,
      destinationId: query.destinationId,
      seatIds,
      journey: leg === 'outbound' ? journey : journeys.outbound,
    }
    const legs: HoldInput[] = roundTrip
      ? [
          outbound,
          {
            tripId,
            originId: returnLeg.originId,
            destinationId: returnLeg.destinationId,
            seatIds: returnLeg.seatIds,
            journey,
          },
        ]
      : [outbound]

    if (holdCovers(hold, legs)) {
      navigate(ROUTES.passengerDetails)
      return
    }
    setBusy(true)
    setFailure(null)
    if (journey && roundTrip) dispatch(setJourney({ leg: 'return', journey }))
    if (order && !order.settled) {
      await cancelCheckout(order.orderId, order.accessToken)
      dispatch(orderReleased())
    }
    try {
      const checkout = await createHold(buildHoldRequest(legs))
      const first = checkout.bookings[0]
      dispatch(
        holdStarted({
          orderId: checkout.orderId,
          bookingId: first.bookingId,
          seatIds: legs[0].seatIds,
          submitted: false,
          accessToken: checkout.accessToken,
          bookingCodes: checkout.bookings.map((b) => b.bookingCode),
          totalAmount: checkout.quotedTotal,
          expiresAt: checkout.expiresAt
            ? Date.parse(checkout.expiresAt)
            : Date.now() + first.holdDurationSeconds * 1000,
          // The backend returns the bookings in the order of the legs sent.
          bookings: checkout.bookings.map((b, i) => ({
            bookingId: b.bookingId,
            tripId: legs[i]?.tripId ?? null,
            seatIds: legs[i]?.seatIds ?? [],
          })),
        }),
      )
      navigate(ROUTES.passengerDetails)
    } catch (error) {
      setFailure(toFailure(error))
    } finally {
      setBusy(false)
    }
  }

  return { submit, busy, failure }
}

/**
 * Step 2, at review: sends the buyer and the passenger of every held seat
 * (`POST /checkouts/{id}/submit`), which makes the order payable, and moves on to payment.
 * Without a live hold (never made, or expired) it sends the traveler back to the seat map.
 */
export function useSubmitCheckout() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { seatIds, purchaser, passengers, hold, returnLeg } = useAppSelector((s) => s.booking)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<BookingFailure | null>(null)

  const submit = async () => {
    if (!hold || hold.expiresAt <= Date.now()) {
      navigate(ROUTES.seatSelection)
      return
    }
    if (hold.submitted) {
      navigate(ROUTES.payment)
      return
    }
    setBusy(true)
    setFailure(null)
    try {
      // Every booking of the order with the passengers of its own seats (two for a round trip).
      const people = [passengers, returnLeg.passengers]
      const bookings = (hold.bookings ?? [{ bookingId: hold.bookingId, tripId: null, seatIds }]).map((b, i) => ({
        bookingId: b.bookingId,
        seatIds: b.seatIds,
        passengers: people[i] ?? {},
      }))
      await submitCheckout(hold.orderId, hold.accessToken, buildSubmitRequest({ purchaser, bookings }))
      dispatch(holdSubmitted())
      navigate(ROUTES.payment)
    } catch (error) {
      setFailure(toFailure(error))
    } finally {
      setBusy(false)
    }
  }

  return { submit, busy, failure }
}
