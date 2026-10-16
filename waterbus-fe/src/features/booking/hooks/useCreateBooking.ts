import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { ApiError } from '../../../services/apiClient'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { holdStarted } from '../bookingSlice'
import { cancelCheckout, createCheckout } from '../services/bookingService'
import { buildCheckoutRequest } from '../utils'

export interface BookingFailure {
  /** Message to show. Empty means the generic "try again" text. */
  message: string
  /** True when a seat was taken meanwhile, so the traveler has to pick another one. */
  seatTaken: boolean
}

/**
 * Creates the checkout at the end of review (this is what holds the seats, until the backend's
 * `expiresAt`) and moves on to payment. A hold that is still running for the same choices is reused.
 */
export function useCreateBooking() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { seatIds, purchaser, passengers, hold, query, order } = useAppSelector((s) => s.booking)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<BookingFailure | null>(null)

  const submit = async (tripId: string, totalVnd: number) => {
    if (hold && hold.expiresAt > Date.now()) {
      navigate(ROUTES.payment)
      return
    }
    setBusy(true)
    setFailure(null)
    // A stale order (the traveler edited something) still holds its seats: release it first.
    if (order && !order.settled) await cancelCheckout(order.orderId, order.accessToken)
    try {
      const checkout = await createCheckout(
        buildCheckoutRequest({
          tripId,
          originId: query.originId,
          destinationId: query.destinationId,
          seatIds,
          purchaser,
          passengers,
        }),
        totalVnd,
      )
      const holdSeconds = checkout.bookings[0]?.holdDurationSeconds ?? 0
      dispatch(
        holdStarted({
          orderId: checkout.orderId,
          accessToken: checkout.accessToken,
          bookingCodes: checkout.bookings.map((b) => b.bookingCode),
          totalAmount: checkout.quotedTotal,
          expiresAt: checkout.expiresAt
            ? Date.parse(checkout.expiresAt)
            : Date.now() + holdSeconds * 1000,
        }),
      )
      navigate(ROUTES.payment)
    } catch (error) {
      setBusy(false)
      const known = error instanceof ApiError && error.status !== 0
      setFailure({
        message: known ? error.message : '',
        seatTaken: known && error.status === 409,
      })
    }
  }

  return { submit, busy, failure }
}
