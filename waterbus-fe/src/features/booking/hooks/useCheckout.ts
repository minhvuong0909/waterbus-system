import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { ApiError } from '../../../services/apiClient'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { holdStarted, holdSubmitted, orderReleased } from '../bookingSlice'
import { cancelCheckout, createHold, submitCheckout } from '../services/bookingService'
import type { Journey } from '../types'
import { buildHoldRequest, buildSubmitRequest } from '../utils'

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

/**
 * Step 1, on the seat page: holds the chosen seats (`POST /checkouts/holds`) before the buyer is
 * asked for anything, then moves on to the passenger details. A hold that still runs for the same
 * seats is reused; a hold left over from other seats is released first.
 */
export function useSeatHold() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { seatIds, hold, order, query } = useAppSelector((s) => s.booking)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<BookingFailure | null>(null)

  const submit = async (tripId: string, totalVnd: number, journey: Journey | undefined) => {
    if (hold && hold.expiresAt > Date.now() && sameSeats(hold.seatIds, seatIds)) {
      navigate(ROUTES.passengerDetails)
      return
    }
    setBusy(true)
    setFailure(null)
    if (order && !order.settled) {
      await cancelCheckout(order.orderId, order.accessToken)
      dispatch(orderReleased())
    }
    try {
      const checkout = await createHold(
        buildHoldRequest({
          tripId,
          originId: query.originId,
          destinationId: query.destinationId,
          seatIds,
          journey,
        }),
        totalVnd,
      )
      const booking = checkout.bookings[0]
      dispatch(
        holdStarted({
          orderId: checkout.orderId,
          bookingId: booking.bookingId,
          seatIds,
          submitted: false,
          accessToken: checkout.accessToken,
          bookingCodes: checkout.bookings.map((b) => b.bookingCode),
          totalAmount: checkout.quotedTotal,
          expiresAt: checkout.expiresAt
            ? Date.parse(checkout.expiresAt)
            : Date.now() + booking.holdDurationSeconds * 1000,
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
  const { seatIds, purchaser, passengers, hold } = useAppSelector((s) => s.booking)
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
      await submitCheckout(
        hold.orderId,
        hold.accessToken,
        buildSubmitRequest({ bookingId: hold.bookingId, seatIds, purchaser, passengers }),
      )
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
