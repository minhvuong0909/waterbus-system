import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { ApiError } from '../../../services/apiClient'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { holdStarted } from '../bookingSlice'
import { createBooking } from '../services/bookingService'
import { toCustomerPhone } from '../utils'

export interface BookingFailure {
  /** Message to show. Empty means the generic "try again" text. */
  message: string
  /** True when a seat was taken meanwhile, so the traveler has to pick another one. */
  seatTaken: boolean
}

/**
 * Creates the booking at the end of checkout (this is what holds the seats for 10 minutes) and
 * moves on to payment. A hold that is still running for the same choices is reused.
 */
export function useCreateBooking() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { seatId, passenger, hold } = useAppSelector((s) => s.booking)
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<BookingFailure | null>(null)

  const submit = async (tripId: string, totalVnd: number) => {
    if (hold && hold.expiresAt > Date.now()) {
      navigate(ROUTES.payment)
      return
    }
    setBusy(true)
    setFailure(null)
    try {
      const booking = await createBooking(
        {
          tripId,
          seatIds: [seatId],
          customerName: passenger.fullName,
          customerEmail: passenger.email,
          customerPhone: toCustomerPhone(passenger),
        },
        totalVnd,
      )
      dispatch(
        holdStarted({
          bookingId: booking.bookingId,
          bookingCode: booking.bookingCode,
          totalAmount: booking.totalAmount,
          expiresAt: Date.now() + booking.holdDurationSeconds * 1000,
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
