import { useEffect } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { holdCleared } from '../../features/booking/bookingSlice'
import { verifyVnPayReturn } from '../../features/payment/services/paymentService'
import { useFetch, useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppDispatch } from '../../store/hooks'
import { t } from '../../i18n'

/** Where VNPay sends the traveler back to (`VnPay:ReturnUrl`): checks the result, then routes on. */
export default function PaymentCallbackPage() {
  useDocumentTitle('Payment Result')
  const { search } = useLocation()
  const dispatch = useAppDispatch()
  const result = useFetch(() => verifyVnPayReturn(search), [search])
  const paid = result.data?.success === true

  // The payment went through, so the seat hold has done its job.
  useEffect(() => {
    if (paid) dispatch(holdCleared())
  }, [paid, dispatch])

  if (result.error) return <ErrorState onRetry={result.retry} />
  if (result.loading || !result.data) return <PageLoader />
  if (result.data.success) return <Navigate to={ROUTES.bookingSuccess} replace />

  const { bookingCode, responseCode, message } = result.data
  return (
    <div
      role="alert"
      className="mx-auto flex min-h-[60vh] max-w-lg flex-col items-center justify-center gap-space-md px-margin py-space-3xl text-center"
    >
      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-coral-glow/15 text-coral-glow">
        <Icon name="payments" className="text-[34px]" />
      </span>
      <h1 className="font-headline-md text-headline-md text-deep-river">
        {t('Payment was not completed')}
      </h1>
      <p className="text-body-md text-on-surface-variant">{message}</p>
      <dl className="grid w-full grid-cols-2 gap-2 rounded-xl bg-mist/70 p-space-md text-left text-xs">
        {bookingCode && (
          <>
            <dt className="text-on-surface-variant">{t('Booking Code:')}</dt>
            <dd className="text-right font-mono font-bold text-deep-river">{bookingCode}</dd>
          </>
        )}
        {responseCode && (
          <>
            <dt className="text-on-surface-variant">{t('Response code')}</dt>
            <dd className="text-right font-mono font-bold text-deep-river">{responseCode}</dd>
          </>
        )}
      </dl>
      <p className="text-xs text-on-surface-variant">
        {t('Nothing was charged if the payment did not go through. Your seat is held only for 10 minutes.')}
      </p>
      <div className="flex flex-wrap justify-center gap-space-sm pt-space-xs">
        <Link
          to={ROUTES.payment}
          className="inline-flex items-center gap-2 rounded-full bg-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-on-primary shadow-sm transition-colors hover:bg-secondary"
        >
          <Icon name="refresh" className="text-[18px]" />
          {t('Try payment again')}
        </Link>
        <Link
          to={ROUTES.seatSelection}
          className="inline-flex items-center rounded-full border border-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-teal-flow transition-colors hover:bg-sand-light"
        >
          {t('Choose another seat')}
        </Link>
      </div>
    </div>
  )
}
