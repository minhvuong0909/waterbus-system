import { Link } from 'react-router-dom'
import { ErrorState, PageLoader } from '../../components/ui'
import BookingStepHeader from '../../features/booking/components/BookingStepHeader'
import JourneyDetailsCard from '../../features/booking/components/JourneyDetailsCard'
import OrderSummary from '../../features/booking/components/OrderSummary'
import PassengerInfoCard from '../../features/booking/components/PassengerInfoCard'
import SummaryAction, { SUMMARY_LINK_CLASS } from '../../features/booking/components/SummaryAction'
import TermsConfirm from '../../features/booking/components/TermsConfirm'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { useSubmitCheckout } from '../../features/booking/hooks/useCheckout'
import { useHoldCountdown } from '../../features/booking/hooks/useHoldCountdown'
import { isRoundTrip } from '../../features/booking/legs'
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
  const { expired } = useHoldCountdown()

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
  const complete = isBookingComplete(
    purchaser,
    seatIds,
    passengers,
    returnTrip ? { seatIds: back.seatIds, passengers: back.passengers } : undefined,
  )

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-lg">
      <BookingStepHeader
        step="Checkout"
        title={t('Review Your Booking')}
        description={t('Confirm your trip, passenger, seat, and fare before proceeding to payment.')}
      />

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
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
            seats={seats}
            totals={totals}
            returnLeg={returnTrip ? { trip: returnTrip, seats: back.seats } : undefined}
            buyerName={purchaser.fullName}
          >
            <SummaryAction
              label={busy ? t('Preparing your order…') : t('Continue to Payment')}
              onClick={submit}
              disabled={!termsAccepted || !complete || busy || expired}
              note={termsAccepted || expired ? undefined : t('Please accept the Waterway Carriage Rules to continue.')}
              error={
                failure && (
                  <>
                    {failure.message ||
                      t('We couldn’t prepare your order just now. Please check your connection and try again.')}{' '}
                    {failure.seatTaken && (
                      <Link to={ROUTES.seatSelection} className="font-semibold underline">
                        {t('Choose another seat')}
                      </Link>
                    )}
                  </>
                )
              }
            />
            <div className="mt-space-sm text-center">
              <Link to={ROUTES.passengerDetails} className={SUMMARY_LINK_CLASS}>
                {t('Back to Passenger Details')}
              </Link>
            </div>
          </OrderSummary>
        </div>
      </div>
    </div>
  )
}
