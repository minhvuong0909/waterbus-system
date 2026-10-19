import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import { useBooking } from '../../features/booking/hooks/useBooking'
import type { BookingStep } from '../../features/booking/types'
import { bookingReference, seatCodes } from '../../features/booking/utils'
import PaymentMethodPanel from '../../features/payment/components/PaymentMethodPanel'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import PaymentOrderSummary from '../../features/payment/components/PaymentOrderSummary'
import { useDocumentTitle } from '../../hooks'
import { useAppSelector } from '../../store/hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function PaymentPage() {
  useDocumentTitle('Complete Your Payment')
  const hold = useAppSelector((s) => s.booking.hold)
  const { loading, error, retry, trip, seats, purchaser, totals } =
    useBooking()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !totals) return <PageLoader />

  const steps: BookingStep[] = [
    { label: 'Trip', detail: trip.lineLabel },
    {
      label: 'Seat',
      detail: seatCodes(seats),
    },
    { label: 'Details', detail: purchaser.fullName },
    { label: 'Checkout', detail: 'Order Confirmed' },
    { label: 'Payment', detail: 'Instant QR / Card' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <div>
        <Breadcrumb
          items={[
            { label: 'Home', to: ROUTES.home },
            { label: 'Booking', to: ROUTES.seatSelection },
            { label: 'Payment' },
          ]}
        />
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
        <div className="self-start md:self-auto lg:hidden">
          <HoldCountdown
            fallback={
              <span className="inline-flex items-center gap-2 rounded-xl bg-surface-container px-space-md py-2 text-xs text-on-surface-variant">
                <Icon name="event_seat" className="text-[16px] text-teal-flow" />
                {t('Your selected seat remains reserved while you complete payment.')}
              </span>
            }
          />
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-8">
          <PaymentMethodPanel
            tripCode={trip.vesselCode}
            amountVnd={totals.totalVnd}
            reference={hold?.bookingCodes[0] ?? bookingReference(seats)}
          />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <PaymentOrderSummary
            trip={trip}
            seats={seats}
            purchaser={purchaser}
            totals={totals}
          />
        </div>
      </div>
    </div>
  )
}
