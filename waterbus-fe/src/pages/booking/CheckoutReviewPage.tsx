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
import { isRoundTrip } from '../../features/booking/legs'
import type { BookingStep } from '../../features/booking/types'
import { addTotals, isBookingComplete } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppSelector } from '../../store/hooks'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

export default function CheckoutReviewPage() {
  useDocumentTitle('Review Your Booking')
  const roundTrip = isRoundTrip(useAppSelector((s) => s.booking.query))
  const outbound = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { trip, seats, seatIds, purchaser, passengers, termsAccepted, setTerms } = outbound
  const { submit, busy, failure } = useSubmitCheckout()

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

  const returnTrip = roundTrip ? back.trip : undefined
  const totals = addTotals(outbound.totals, roundTrip ? back.totals : undefined)
  const seatCount = seats.length + (returnTrip ? back.seats.length : 0)

  const steps: BookingStep[] = [
    { label: 'Trip', detail: returnTrip ? t('Round trip') : trip.lineLabel },
    { label: 'Seat', detail: t('{count} Seats Selected', { count: seatCount }) },
    { label: 'Passengers', detail: purchaser.fullName },
    { label: 'Checkout', detail: 'Review Order' },
    { label: 'Payment', detail: 'VNPay' },
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
        <div className="self-start md:self-auto lg:hidden">
          <HoldCountdown />
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-8">
          <JourneyDetailsCard trip={trip} seats={seats} title={returnTrip ? t('Outbound trip') : undefined} />
          {returnTrip && (
            <JourneyDetailsCard
              trip={returnTrip}
              seats={back.seats}
              title={t('Return trip')}
              editTo={`${ROUTES.seatSelection}?leg=return`}
            />
          )}
          <PassengerInfoCard
            purchaser={purchaser}
            seats={seats}
            passengers={passengers}
            title={returnTrip ? t('Passengers · outbound trip') : undefined}
            showBuyer={!returnTrip}
          />
          {returnTrip && (
            <PassengerInfoCard
              purchaser={purchaser}
              seats={back.seats}
              passengers={back.passengers}
              title={t('Passengers · return trip')}
            />
          )}
          <TermsConfirm checked={termsAccepted} onChange={setTerms} />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <OrderSummary
            trip={trip}
            returnTrip={returnTrip}
            totals={totals}
            canContinue={
              termsAccepted &&
              isBookingComplete(
                purchaser,
                seatIds,
                passengers,
                returnTrip ? { seatIds: back.seatIds, passengers: back.passengers } : undefined,
              )
            }
            busy={busy}
            failure={failure}
            onContinue={submit}
          />
        </div>
      </div>
    </div>
  )
}
