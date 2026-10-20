import type { ReactNode } from 'react'
import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import CrossingSummary from '../../features/booking/components/CrossingSummary'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import PassengerFormCard from '../../features/booking/components/PassengerFormCard'
import { copyPassengersToReturn } from '../../features/booking/bookingSlice'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { isRoundTrip } from '../../features/booking/legs'
import type { BookingStep } from '../../features/booking/types'
import { addTotals, isBookingComplete, seatCodes } from '../../features/booking/utils'
import type { TripDetail } from '../../features/trips/types'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppDispatch, useAppSelector } from '../../store/hooks'
import { t } from '../../i18n'

/** "Outbound trip · Bach Dang → Linh Dong · 15:00", above the passengers of that leg. */
function LegHeading({ title, trip, action }: { title: string; trip: TripDetail; action?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 px-1">
      <div>
        <h2 className="font-headline-sm text-lg font-bold text-deep-river">{title}</h2>
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
  const roundTrip = isRoundTrip(useAppSelector((s) => s.booking.query))
  const outbound = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { trip, seats, seatIds, purchaser, passengers, editPurchaser, editSeatPassenger } = outbound

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
  const seatDetail = returnLeg ? `${seatCodes(seats)} · ${seatCodes(returnLeg.seats)}` : seatCodes(seats)
  const outboundNamed = seats.some((seat) => passengers[seat.id]?.fullName.trim())

  const steps: BookingStep[] = [
    { label: 'Trip', detail: 'Trip' },
    { label: 'Seat', detail: t('Seat ({id})', { id: seatDetail }) },
    { label: 'Passenger Details', detail: 'Passenger Details' },
    { label: 'Checkout', detail: 'Checkout' },
    { label: 'Payment', detail: 'Payment' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'Booking', to: ROUTES.seatSelection },
          { label: 'Passenger Details' },
        ]}
      />
      <div className="mt-space-md">
        <BookingProgress steps={steps} current={2} variant="circles" />
      </div>

      <div className="mt-space-lg flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <h1 className="font-headline-xl text-headline-xl-mobile tracking-tight text-deep-river md:text-headline-lg">
            {t('Passenger Details')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {roundTrip
              ? t('Enter who travels on each seat, for both trips.')
              : t('Please enter traveler information for this crossing.')}
          </p>
        </div>
        <div className="self-start md:self-auto lg:hidden">
          <HoldCountdown />
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-xl lg:col-span-7">
          <PassengerFormCard
            purchaser={purchaser}
            seats={seats}
            passengers={passengers}
            onPurchaser={editPurchaser}
            onPassenger={editSeatPassenger}
            showBuyer={!returnLeg}
            heading={returnLeg ? <LegHeading title={t('Outbound trip')} trip={trip} /> : undefined}
          />
          {returnLeg && (
            <PassengerFormCard
              purchaser={purchaser}
              seats={returnLeg.seats}
              passengers={back.passengers}
              onPurchaser={editPurchaser}
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
                      <Icon name="content_copy" className="text-[16px]" />
                      {t('Same passengers as outbound')}
                    </button>
                  }
                />
              }
            />
          )}
        </div>
        <div className="lg:col-span-5 lg:self-stretch">
          <CrossingSummary
            trip={trip}
            seats={seats}
            totals={totals}
            returnLeg={returnLeg}
            canContinue={isBookingComplete(
              purchaser,
              seatIds,
              passengers,
              returnLeg ? { seatIds: back.seatIds, passengers: back.passengers } : undefined,
            )}
          />
        </div>
      </div>
    </div>
  )
}
