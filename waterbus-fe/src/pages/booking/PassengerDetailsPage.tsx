import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingStepHeader from '../../features/booking/components/BookingStepHeader'
import OrderSummary from '../../features/booking/components/OrderSummary'
import SummaryAction, { SUMMARY_LINK_CLASS } from '../../features/booking/components/SummaryAction'
import PassengerFormCard from '../../features/booking/components/PassengerFormCard'
import BuyerCard from '../../features/booking/components/BuyerCard'
import PassengerFormNote from '../../features/booking/components/PassengerFormNote'
import { copyPassengersToReturn, holdCleared, setBuyerTravels } from '../../features/booking/bookingSlice'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { useHoldCountdown } from '../../features/booking/hooks/useHoldCountdown'
import { isRoundTrip } from '../../features/booking/legs'
import { addTotals, isBookingComplete } from '../../features/booking/utils'
import type { TripDetail } from '../../features/trips/types'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppDispatch, useAppSelector } from '../../store/hooks'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

/** "Outbound trip · Bach Dang → Linh Dong · 15:00", above the passengers of that leg. */
function LegHeading({ title, trip, action }: { title: string; trip: TripDetail; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-1">
      <div>
        <h2 className="font-display text-lg font-bold text-deep-river">{title}</h2>
        <p className="text-xs text-on-surface-variant">
          {trip.originShortName} → {trip.destinationShortName} · {trip.dateShort} · {trip.departTime}
        </p>
      </div>
      {action}
    </div>
  )
}

export default function PassengerDetailsPage() {
  useDocumentTitle('Passenger Details')
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const roundTrip = isRoundTrip(useAppSelector((s) => s.booking.query))
  // Once sent with the order (review step), the backend no longer accepts changes to these details.
  const submitted = useAppSelector((s) => s.booking.hold?.submitted ?? false)
  const buyerTravels = useAppSelector((s) => s.booking.buyerTravels)
  // An expired hold has released the seats: the summary says so, and the step cannot go on.
  const { expired } = useHoldCountdown()
  const outbound = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { trip, seats, seatIds, purchaser, passengers, editPurchaser, editSeatPassenger } = outbound

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

  const totals = addTotals(outbound.totals, roundTrip ? back.totals : undefined)
  const returnLeg = roundTrip && back.trip ? { trip: back.trip, seats: back.seats } : undefined
  const outboundNamed = seats.some((seat) => passengers[seat.id]?.fullName.trim())
  const complete = isBookingComplete(
    purchaser,
    seatIds,
    passengers,
    returnLeg ? { seatIds: back.seatIds, passengers: back.passengers } : undefined,
  )

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-lg">
      <BookingStepHeader
        step="Passengers"
        title={t('Passenger Details')}
        description={
          roundTrip
            ? t('Enter who travels on each seat, for both trips.')
            : t('Please enter traveler information for this crossing.')
        }
      />

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-8">
          {submitted && (
            <div
              role="status"
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-signal-amber/40 bg-signal-amber/10 px-4 py-3 text-sm text-deep-river"
            >
              <span>{t('These details were sent with your order and can no longer be edited here.')}</span>
              {/* A new hold for the same seats gives a new order whose details can be sent again. */}
              <button
                type="button"
                onClick={() => {
                  dispatch(holdCleared())
                  navigate(ROUTES.seatSelection)
                }}
                className="inline-flex min-h-[36px] items-center gap-1.5 whitespace-nowrap rounded-full bg-deep-river px-3.5 text-xs font-semibold text-white hover:opacity-90"
              >
                <Icon name="edit" className="text-base" />
                {t('Edit details')}
              </button>
            </div>
          )}
          <fieldset disabled={submitted} className="m-0 min-w-0 space-y-space-xl border-0 p-0">
            <BuyerCard
              purchaser={purchaser}
              onPurchaser={editPurchaser}
              buyerTravels={buyerTravels}
              onBuyerTravels={(value) => dispatch(setBuyerTravels(value))}
              seatCode={seats[0]?.code}
            />
            <PassengerFormCard
              purchaser={purchaser}
              seats={seats}
              passengers={passengers}
              onPassenger={editSeatPassenger}
              buyerSeatId={buyerTravels ? seats[0]?.id : undefined}
              heading={returnLeg ? <LegHeading title={t('Outbound trip')} trip={trip} /> : undefined}
            />
            {returnLeg && (
              <PassengerFormCard
                purchaser={purchaser}
                seats={returnLeg.seats}
                passengers={back.passengers}
                onPassenger={back.editSeatPassenger}
                heading={
                  <LegHeading
                    title={t('Return trip')}
                    trip={returnLeg.trip}
                    action={
                      <button
                        type="button"
                        disabled={!outboundNamed}
                        onClick={() => dispatch(copyPassengersToReturn())}
                        className="inline-flex min-h-[36px] items-center gap-1.5 rounded-full border border-teal-flow px-3.5 text-xs font-semibold text-teal-flow transition-colors hover:bg-teal-flow hover:text-white disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        <Icon name="content_copy" className="text-base" />
                        {t('Same passengers as outbound')}
                      </button>
                    }
                  />
                }
              />
            )}
            <PassengerFormNote />
          </fieldset>
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <OrderSummary trip={trip} seats={seats} totals={totals} returnLeg={returnLeg}>
            <SummaryAction
              label={t('Continue to Checkout')}
              to={ROUTES.checkoutReview}
              disabled={!complete || expired}
              note={
                complete || expired
                  ? undefined
                  : t('Enter a name for every passenger and the buyer’s name and email to continue.')
              }
            />
            <div className="mt-space-sm flex flex-wrap justify-center gap-x-space-md gap-y-1">
              <Link to={ROUTES.seatSelection} className={SUMMARY_LINK_CLASS}>
                {returnLeg ? t('Change outbound seats') : t('Change Seat')}
              </Link>
              {returnLeg && (
                <Link to={`${ROUTES.seatSelection}?leg=return`} className={SUMMARY_LINK_CLASS}>
                  {t('Change return seats')}
                </Link>
              )}
            </div>
          </OrderSummary>
        </div>
      </div>
    </div>
  )
}
