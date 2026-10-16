import { Link, Navigate, useLocation } from 'react-router-dom'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { useOrderConfirmation } from '../../features/payment/hooks/useOrderConfirmation'
import { verifyVnPayReturn } from '../../features/payment/services/paymentService'
import { useFetch, useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const ACTION =
  'inline-flex items-center gap-2 rounded-full bg-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-on-primary shadow-sm transition-colors hover:bg-secondary'
const ACTION_OUTLINE =
  'inline-flex items-center rounded-full border border-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-teal-flow transition-colors hover:bg-sand-light'

/**
 * Where VNPay sends the traveler back to (`VnPay:ReturnUrl`). The redirect only says what the
 * browser was sent with; the payment counts when the backend reports the order as confirmed
 * (BR-PAY-01), so this page waits for that before showing the tickets.
 */
export default function PaymentCallbackPage() {
  useDocumentTitle('Payment Result')
  const { search } = useLocation()
  const result = useFetch(() => verifyVnPayReturn(search), [search])
  const confirmation = useOrderConfirmation()
  const { phase } = confirmation

  if (result.error) return <ErrorState onRetry={result.retry} />
  if (phase === 'confirmed') return <Navigate to={ROUTES.bookingSuccess} replace />
  if (result.loading || !result.data) return <PageLoader />

  const { success, bookingCode, responseCode, message } = result.data

  if (phase === 'none') {
    return (
      <Notice
        icon="receipt_long"
        title={t('We could not find your order')}
        body={t('Open your booking from the confirmation email, or start a new booking.')}
      >
        <Link to={ROUTES.home} className={ACTION}>
          {t('Back to home')}
        </Link>
      </Notice>
    )
  }

  if (success && phase === 'pending') {
    return (
      <div role="status" className="mx-auto max-w-lg px-margin py-space-3xl text-center">
        <PageLoader />
        <h1 className="font-headline-md text-headline-md text-deep-river">
          {t('Confirming your payment…')}
        </h1>
        <p className="mt-space-sm text-body-md text-on-surface-variant">
          {t('The bank has sent you back. We are waiting for the payment to be confirmed, please do not close this page.')}
        </p>
      </div>
    )
  }

  if (success && phase === 'timeout') {
    return (
      <Notice
        icon="hourglass_top"
        title={t('Your payment is not confirmed yet')}
        body={t('If money was taken from your account, the tickets will appear once the bank confirms it. You can check again or ask us to verify with the bank.')}
      >
        <button type="button" onClick={confirmation.recheck} className={ACTION}>
          <Icon name="refresh" className="text-[18px]" />
          {t('Check again')}
        </button>
        <button type="button" onClick={confirmation.reconcile} className={ACTION_OUTLINE}>
          {t('Verify with the bank')}
        </button>
      </Notice>
    )
  }

  return (
    <Notice
      icon="payments"
      title={t('Payment was not completed')}
      body={phase === 'failed' ? t('This order has expired or was cancelled, so its seats were released.') : message}
      details={[
        [t('Booking Code:'), bookingCode],
        [t('Response code'), responseCode],
      ]}
    >
      {phase === 'failed' ? (
        <Link to={ROUTES.seatSelection} className={ACTION}>
          {t('Choose seats again')}
        </Link>
      ) : (
        <>
          <Link to={ROUTES.payment} className={ACTION}>
            <Icon name="refresh" className="text-[18px]" />
            {t('Try payment again')}
          </Link>
          <Link to={ROUTES.seatSelection} className={ACTION_OUTLINE}>
            {t('Choose another seat')}
          </Link>
        </>
      )}
    </Notice>
  )
}

function Notice({
  icon,
  title,
  body,
  details = [],
  children,
}: {
  icon: string
  title: string
  body: string
  details?: [string, string][]
  children: React.ReactNode
}) {
  const shown = details.filter(([, value]) => value)
  return (
    <div
      role="alert"
      className="mx-auto flex min-h-[60vh] max-w-lg flex-col items-center justify-center gap-space-md px-margin py-space-3xl text-center"
    >
      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-coral-glow/15 text-coral-glow">
        <Icon name={icon} className="text-[34px]" />
      </span>
      <h1 className="font-headline-md text-headline-md text-deep-river">{title}</h1>
      <p className="text-body-md text-on-surface-variant">{body}</p>
      {shown.length > 0 && (
        <dl className="grid w-full grid-cols-2 gap-2 rounded-xl bg-mist/70 p-space-md text-left text-xs">
          {shown.map(([label, value]) => (
            <div key={label} className="contents">
              <dt className="text-on-surface-variant">{label}</dt>
              <dd className="text-right font-mono font-bold text-deep-river">{value}</dd>
            </div>
          ))}
        </dl>
      )}
      <div className="flex flex-wrap justify-center gap-space-sm pt-space-xs">{children}</div>
    </div>
  )
}
