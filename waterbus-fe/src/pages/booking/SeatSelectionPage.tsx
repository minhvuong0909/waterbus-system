import { Link, useSearchParams } from 'react-router-dom'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingStepHeader from '../../features/booking/components/BookingStepHeader'
import NoTripSelected from '../../features/booking/components/NoTripSelected'
import OrderSummary from '../../features/booking/components/OrderSummary'
import SeatLegend from '../../features/booking/components/SeatLegend'
import SeatMapView from '../../features/booking/components/SeatMapView'
import SummaryAction, { SUMMARY_LINK_CLASS } from '../../features/booking/components/SummaryAction'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { RETURN_RESULTS, useSeatHold } from '../../features/booking/hooks/useCheckout'
import { isRoundTrip } from '../../features/booking/legs'
import type { Leg } from '../../features/booking/legs'
import { MAX_SEATS_PER_BOOKING, seatCodes } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { useAppSelector } from '../../store/hooks'

export default function SeatSelectionPage() {
  useDocumentTitle('Choose Your Seat')
  const [searchParams] = useSearchParams()
  const query = useAppSelector((s) => s.booking.query)
  const outboundSeatCount = useAppSelector((s) => s.booking.seatIds.length)
  const roundTrip = isRoundTrip(query)
  // `?leg=return`: the seats of the return trip of a round trip.
  const leg: Leg = roundTrip && searchParams.get('leg') === 'return' ? 'return' : 'outbound'
  const { noTrip, loading, error, retry, trip, seatMap, seats, seatIds, seatLimitReached, totals, toggleSeat } =
    useBooking({ live: true, leg })
  const hold = useSeatHold()

  if (noTrip) return <NoTripSelected />
  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !seatMap || !totals) return <PageLoader />
  // BR-TIME-01: sales close before the trip leaves its first pier; seats can no longer be picked.
  const salesOpen = seatMap.canBook !== false
  const takenSeats = seats.filter((seat) => seat.status !== 'available')
  // An empty selection, or one with a seat somebody else took, must not reach checkout.
  const canContinue = salesOpen && seats.length > 0 && takenSeats.length === 0
  const heldOnContinue = (
    <span className="inline-flex items-center gap-1 rounded-xl bg-surface-container px-space-md py-2 text-xs font-semibold text-teal-flow">
      <Icon name="timer" className="text-sm" /> {t('Seats are held once you continue')}
    </span>
  )

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-lg">
      <BookingStepHeader
        step="Seat"
        title={
          roundTrip
            ? leg === 'return'
              ? t('Choose your return seats')
              : t('Choose your outbound seats')
            : t('Choose Your Seat')
        }
        description={
          leg === 'return' && outboundSeatCount > 0
            ? t('You picked {count} seats for the outbound trip.', { count: outboundSeatCount })
            : t('Tap an available seat to select it. Up to {max} seats per booking.', {
                max: MAX_SEATS_PER_BOOKING,
              })
        }
        countdownFallback={heldOnContinue}
      />

      {!salesOpen && (
        <p
          role="alert"
          className="mt-space-md flex items-center gap-2 rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-space-md py-2 text-sm text-deep-river"
        >
          <Icon name="block" className="text-lg text-coral-glow" />
          {t('Ticket sales are closed for this trip. Please choose another trip.')}
        </p>
      )}

      {takenSeats.length > 0 && (
        <p
          role="alert"
          className="mt-space-md rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-space-md py-2 text-sm text-deep-river"
        >
          {t('Seat {id} was just taken by another traveler. Please choose another seat.', {
            id: seatCodes(takenSeats),
          })}
        </p>
      )}

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="rounded-2xl bg-white p-space-lg shadow-card lg:col-span-8">
          <div className="mb-space-md flex flex-wrap items-start justify-between gap-space-md">
            <div>
              <h2 className="font-display text-headline-sm text-deep-river">{seatMap.vesselName}</h2>
              <p className="text-xs text-on-surface-variant">
                {t('{count} seats • live availability', { count: seatMap.seats.length })}
              </p>
            </div>
            <SeatLegend />
          </div>
          <SeatMapView
            seatMap={seatMap}
            selectedIds={seatIds}
            onSelect={salesOpen ? toggleSeat : () => undefined}
          />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <OrderSummary trip={trip} seats={seats} totals={totals} countdownFallback={heldOnContinue}>
            {seatLimitReached && (
              <p role="status" className="mt-space-sm text-xs text-coral-glow">
                {t('You can book up to {max} seats at a time.', { max: MAX_SEATS_PER_BOOKING })}
              </p>
            )}
            <SummaryAction
              label={
                hold.busy
                  ? t('Holding your seat…')
                  : roundTrip && leg === 'outbound'
                    ? t('Continue to the return trip')
                    : t('Continue to Passenger Details')
              }
              onClick={() => hold.submit(leg, trip.id, seatMap.journey)}
              disabled={!canContinue || hold.busy}
              note={salesOpen ? undefined : t('Ticket sales are closed for this trip.')}
              error={
                hold.failure &&
                (hold.failure.message ||
                  t('We couldn’t hold your seat just now. Please check your connection and try again.'))
              }
            />
            <div className="mt-space-sm text-center">
              <Link to={leg === 'return' ? RETURN_RESULTS : ROUTES.searchResults} className={SUMMARY_LINK_CLASS}>
                {t('Change Trip')}
              </Link>
            </div>
          </OrderSummary>
        </div>
      </div>
    </div>
  )
}
