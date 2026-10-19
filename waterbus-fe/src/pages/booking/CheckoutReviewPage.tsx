import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import BookingProgress from '../../features/booking/components/BookingProgress'
import {
  JourneyDetailsCard,
  OrderSummary,
  PassengerInfoCard,
  TermsConfirm,
} from '../../features/booking/components/CheckoutParts'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { useSubmitCheckout } from '../../features/booking/hooks/useCheckout'
import type { BookingStep } from '../../features/booking/types'
import { isBookingComplete } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function CheckoutReviewPage() {
  useDocumentTitle('Review Your Booking')
  const {
    loading,
    error,
    retry,
    trip,
    seats,
    seatIds,
    purchaser,
    passengers,
    totals,
    termsAccepted,
    setTerms,
  } = useBooking()
  const { submit, busy, failure } = useSubmitCheckout()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !totals) return <PageLoader />

  const steps: BookingStep[] = [
    { label: 'Trip', detail: trip.lineLabel },
    { label: 'Seat', detail: t('{count} Seats Selected', { count: seats.length }) },
    { label: 'Details', detail: purchaser.fullName },
    { label: 'Checkout', detail: 'Review Order' },
    { label: 'Payment', detail: 'Instant QR / Card' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <div>
        <Breadcrumb
          items={[
            { label: 'Home', to: ROUTES.home },
            { label: 'Booking', to: ROUTES.seatSelection },
            { label: 'Checkout' },
          ]}
        />
      </div>

      <div className="mt-space-md">
        <BookingProgress steps={steps} current={3} variant="bars" />
      </div>

      <div className="mt-space-xl flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <h1 className="font-headline-xl text-headline-xl-mobile tracking-tight text-deep-river md:text-headline-xl">
            {t('Review Your Booking')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {t('Confirm your trip, passenger, seat, and fare before proceeding to payment.')}
          </p>
        </div>
        <div className="self-start md:self-auto">
          <HoldCountdown />
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-8">
          <JourneyDetailsCard trip={trip} seats={seats} />
          <PassengerInfoCard purchaser={purchaser} seats={seats} passengers={passengers} />
          <TermsConfirm checked={termsAccepted} onChange={setTerms} />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <OrderSummary
            trip={trip}
            totals={totals}
            canContinue={termsAccepted && isBookingComplete(purchaser, seatIds, passengers)}
            busy={busy}
            failure={failure}
            onContinue={submit}
          />
        </div>
      </div>
    </div>
  )
}
