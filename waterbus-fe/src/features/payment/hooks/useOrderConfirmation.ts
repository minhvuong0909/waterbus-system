import { useCallback, useEffect, useState } from 'react'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { orderSettled } from '../../booking/bookingSlice'
import { getCheckout } from '../../booking/services/bookingService'
import type { CheckoutDetail } from '../../booking/types'
import { reconcilePayment } from '../services/paymentService'
import { isPaidAfterExpiry } from '../utils'

/**
 * pending: still waiting for the backend; confirmed: paid; failed: expired or cancelled; timeout: gave
 * up waiting; paidLate: money was taken after the order expired, so no ticket and a refund is due.
 */
export type ConfirmationPhase = 'none' | 'pending' | 'confirmed' | 'failed' | 'timeout' | 'paidLate'

const POLL_MS = 3000
const MAX_POLLS = 40

/**
 * Waits for the backend to report the order as paid (BR-PAY-01). The browser never decides that a
 * payment succeeded: the status comes from `GET /checkouts/{id}`, which changes when VNPay's IPN
 * is verified. Polls every few seconds, then gives up and lets the traveler ask for a reconcile.
 */
export function useOrderConfirmation() {
  const dispatch = useAppDispatch()
  const order = useAppSelector((s) => s.booking.order)
  const [phase, setPhase] = useState<ConfirmationPhase>(order ? 'pending' : 'none')
  const [detail, setDetail] = useState<CheckoutDetail | null>(null)
  const [round, setRound] = useState(0)

  const orderId = order?.orderId
  const accessToken = order?.accessToken ?? null

  useEffect(() => {
    if (!orderId) return
    let cancelled = false
    let timer: number | undefined
    let polls = 0

    const poll = async () => {
      polls += 1
      try {
        const result = await getCheckout(orderId, accessToken)
        if (cancelled) return
        setDetail(result)
        if (result.status === 'Confirmed') {
          setPhase('confirmed')
          dispatch(orderSettled())
          return
        }
        if (result.status === 'Expired' || result.status === 'Cancelled') {
          setPhase(isPaidAfterExpiry(result) ? 'paidLate' : 'failed')
          dispatch(orderSettled())
          return
        }
      } catch {
        // A failed read is retried like a "not yet" answer.
      }
      if (cancelled) return
      if (polls >= MAX_POLLS) setPhase('timeout')
      else timer = window.setTimeout(poll, POLL_MS)
    }
    void poll()

    return () => {
      cancelled = true
      window.clearTimeout(timer)
    }
  }, [orderId, accessToken, round, dispatch])

  const recheck = useCallback(() => {
    setPhase('pending')
    setRound((n) => n + 1)
  }, [])

  /** Asks the backend to query VNPay about the open payment, then checks the order again. */
  const reconcile = useCallback(async () => {
    const attempt = detail?.payments.find(
      (p) => p.type === 'Payment' && (p.status === 'Requested' || p.status === 'Processing'),
    )
    if (orderId && attempt) {
      try {
        await reconcilePayment(orderId, attempt.transactionId, accessToken)
      } catch {
        // The check below shows whatever state the order is in.
      }
    }
    setPhase('pending')
    setRound((n) => n + 1)
  }, [detail, orderId, accessToken])

  return { phase, detail, order, recheck, reconcile }
}
