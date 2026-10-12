import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import { useBooking } from '../../features/booking/hooks/useBooking'
import type { BookingStep } from '../../features/booking/types'
import { bookingReference, seatTags } from '../../features/booking/utils'
import PaymentMethodPanel from '../../features/payment/components/PaymentMethodPanel'
import PaymentOrderSummary from '../../features/payment/components/PaymentOrderSummary'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function PaymentPage() {
  useDocumentTitle('Complete Your Payment')
  const { loading, error, retry, trip, seat, seatId, passenger, totals, paymentMethod, setMethod } =
    useBooking()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !totals) return <PageLoader />

  const steps: BookingStep[] = [
    { label: 'Trip', detail: trip.lineLabel },
    {
      label: 'Seat',
      detail: `${seatId} ${seatTags(seat) || t('Aisle')}`,
    },
    { label: 'Details', detail: passenger.fullName },
    { label: 'Checkout', detail: 'Order Confirmed' },
    { label: 'Payment', detail: 'Instant QR / Card' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <div className="flex flex-wrap items-center justify-between gap-space-sm">
        <Breadcrumb
          items={[
            { label: 'Home', to: ROUTES.home },
            { label: 'Booking', to: ROUTES.seatSelection },
            { label: 'Payment' },
          ]}
        />
        <span className="inline-flex items-center gap-2 rounded-full bg-secondary-container/60 px-3 py-1 text-xs font-semibold text-on-secondary-container">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('River Crossing {vesselCode} • Final Step', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="mt-space-md">
        <BookingProgress steps={steps} current={4} variant="circles" numbered />
      </div>

      <div className="mt-space-lg flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <h1 className="font-headline-xl text-headline-xl-mobile tracking-tight text-deep-river md:text-headline-lg">
            {t('Complete Your Payment')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {t(
              'Choose your payment method to finalize your river crossing reservation and issue your ticket.',
            )}
          </p>
        </div>
        <span className="inline-flex items-center gap-2 self-start rounded-xl bg-surface-container px-space-md py-2 text-xs text-on-surface-variant md:self-auto">
          <Icon name="event_seat" className="text-[16px] text-teal-flow" />
          {t('Your selected seat remains reserved while you complete payment.')}
        </span>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-8">
          <PaymentMethodPanel
            tripCode={trip.vesselCode}
            method={paymentMethod}
            onMethod={setMethod}
            amountVnd={totals.totalVnd}
            reference={bookingReference(seatId)}
          />
        </div>
        <div className="lg:col-span-4">
          <PaymentOrderSummary
            trip={trip}
            seat={seat}
            seatId={seatId}
            passenger={passenger}
            totals={totals}
          />
        </div>
      </div>
    </div>
  )
}
