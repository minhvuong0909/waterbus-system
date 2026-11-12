import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Button, Modal } from '../../../components/ui'
import { holdExtended } from '../../booking/bookingSlice'
import SummaryAction, { SUMMARY_LINK_CLASS } from '../../booking/components/SummaryAction'
import { useCancelCheckout } from '../../booking/hooks/useCheckout'
import { useHoldCountdown } from '../../booking/hooks/useHoldCountdown'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { createPaymentUrl } from '../services/paymentService'

/**
 * Foot of the order summary on the payment step: opens the VNPay page, or cancels the order and
 * releases the seats. BR-PAY-01: paying only sends the traveler to the gateway; the ticket is
 * issued when the backend verifies the payment, so there is no "I have paid" here.
 */
export default function PaymentActions() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const order = useAppSelector((s) => s.booking.order)
  const { hold, expired } = useHoldCountdown()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const cancelling = useCancelCheckout()

  const pay = async () => {
    if (!hold) return
    setBusy(true)
    setFailed(false)
    try {
      const { url, expiresAt } = await createPaymentUrl(hold.orderId, order?.accessToken ?? hold.accessToken)
      if (expiresAt !== null) dispatch(holdExtended(expiresAt))
      if (url.startsWith('/')) navigate(url)
      else window.location.assign(url)
    } catch {
      setBusy(false)
      setFailed(true)
    }
  }

  return (
    <>
      <SummaryAction
        label={busy ? t('Opening the payment page…') : t('Pay with VNPay')}
        onClick={pay}
        disabled={busy || expired || !hold}
        note={t('You will be redirected to VNPay. Your tickets are issued once the payment is confirmed.')}
        error={failed && t('We couldn’t open the payment page. Nothing was charged, please try again.')}
      />
      <div className="mt-space-sm flex flex-wrap items-center justify-center gap-x-space-md gap-y-1">
        <Link to={ROUTES.checkoutReview} className={SUMMARY_LINK_CLASS}>
          {t('Back to Checkout')}
        </Link>
        {hold && !expired && (
          <button
            type="button"
            onClick={() => setConfirmCancel(true)}
            className="text-sm font-semibold text-coral-glow hover:underline"
          >
            {t('Cancel and release seats')}
          </button>
        )}
      </div>
      {confirmCancel && (
        <Modal title={t('Cancel this booking?')} onClose={() => setConfirmCancel(false)}>
          <p className="text-sm text-on-surface-variant">
            {t('Your held seats are released for other travelers. Nothing has been charged.')}
          </p>
          <div className="mt-space-md flex flex-wrap justify-end gap-space-sm">
            <Button variant="neutral" onClick={() => setConfirmCancel(false)} disabled={cancelling.busy}>
              {t('Keep my seats')}
            </Button>
            <Button variant="danger" onClick={cancelling.cancel} disabled={cancelling.busy}>
              {cancelling.busy ? t('Releasing…') : t('Cancel booking')}
            </Button>
          </div>
        </Modal>
      )}
    </>
  )
}
