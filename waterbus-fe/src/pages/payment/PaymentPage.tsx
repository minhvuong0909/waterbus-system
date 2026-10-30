import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import { useBooking } from '../../features/booking/hooks/useBooking'
import type { BookingStep } from '../../features/booking/types'
import { addTotals, seatCodes } from '../../features/booking/utils'
import { isRoundTrip } from '../../features/booking/legs'
import PaymentMethodPanel from '../../features/payment/components/PaymentMethodPanel'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import PaymentOrderSummary from '../../features/payment/components/PaymentOrderSummary'
import { useDocumentTitle } from '../../hooks'
import { useAppSelector } from '../../store/hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

export default function PaymentPage() {
  useDocumentTitle('Complete Your Payment')
  const hold = useAppSelector((s) => s.booking.hold)
  const roundTrip = isRoundTrip(useAppSelector((s) => s.booking.query))
  const outbound = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { trip, seats, purchaser } = outbound

  if (outbound.noTrip) return <NoTripSelected />
  const error = outbound.error ?? back.error
  if (error) {
    return (
      <ErrorState
        onRetry={() => {
          outbound.retry()
          back.retry()
        }}
      />
    )
  }

  const returnReady = !roundTrip || (back.trip && back.totals)
  if (outbound.loading || back.loading || !trip || !outbound.totals || !returnReady) return <PageLoader />

  // One payment for the whole order: both legs of a round trip (BR-COM-02, BR-PAY-04).
  const totals = addTotals(outbound.totals, roundTrip ? back.totals : undefined)
  const returnLeg = roundTrip && back.trip ? { trip: back.trip, seats: back.seats } : undefined

  const steps: BookingStep[] = [
    { label: 'Trip', detail: trip.lineLabel },
    {
      label: 'Seat',
      detail: returnLeg ? `${seatCodes(seats)} · ${seatCodes(returnLeg.seats)}` : seatCodes(seats),
    },
    { label: 'Passengers', detail: purchaser.fullName },
    { label: 'Checkout', detail: 'Awaiting payment' },
    { label: 'Payment', detail: 'VNPay' },
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
            amountVnd={totals.totalVnd}
            reference={hold?.bookingCodes.join(', ') ?? ''}
          />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <PaymentOrderSummary
            trip={trip}
            seats={seats}
            purchaser={purchaser}
            totals={totals}
            returnLeg={returnLeg}
          />
        </div>
      </div>
    </div>
  )
}
