import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import SeatMapView, { SeatLegend } from '../../features/booking/components/SeatMapView'
import {
  BookingSummary,
  TripStrip,
} from '../../features/booking/components/SeatSelectionParts'
import { useSeatHold } from '../../features/booking/hooks/useCheckout'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { MAX_SEATS_PER_BOOKING, seatCodes } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function SeatSelectionPage() {
  useDocumentTitle('Choose Your Seat')
  const { loading, error, retry, trip, seatMap, seats, seatIds, seatLimitReached, totals, toggleSeat } =
    useBooking({ live: true })
  const hold = useSeatHold()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !seatMap || !totals) return <PageLoader />

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'Search Results', to: ROUTES.searchResults },
          { label: t('Trip {code}', { code: trip.vesselCode }), to: ROUTES.tripDetail },
          { label: 'Choose Your Seat' },
        ]}
      />
      <div className="mt-2 flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <span className="inline-flex items-center gap-1.5 rounded-full border border-teal-flow/20 bg-secondary-container/60 px-2.5 py-0.5 text-xs font-semibold text-teal-flow">
            <Icon name="airline_seat_recline_extra" className="text-[14px]" />
            {t('Step 2 of 5: Seat Selection')}
          </span>
          <h1 className="mt-1 font-headline-lg text-headline-lg tracking-tight text-deep-river">
            {t('Choose Your Seat')}
          </h1>
        </div>
        <div className="self-start md:self-auto">
          <HoldCountdown
            fallback={
              <span className="inline-flex items-center gap-1 rounded-xl bg-white px-space-md py-2 text-xs font-semibold text-teal-flow shadow-sm">
                <Icon name="timer" className="text-[15px]" /> {t('Cart holds for 10:00')}
              </span>
            }
          />
        </div>
      </div>

      <div className="mt-space-md space-y-space-md">
        <TripStrip trip={trip} seats={seats} totalVnd={totals.totalVnd} />
        <p className="flex items-center gap-2 rounded-xl border border-teal-flow/20 bg-sand-light/60 px-space-md py-2 text-xs text-on-surface-variant">
          <Icon name="touch_app" className="text-[18px] text-teal-flow" />
          {t('Tap an available seat to select it. Up to {max} seats per booking.', {
            max: MAX_SEATS_PER_BOOKING,
          })}
        </p>
      </div>

      {seats.some((s) => s.status !== 'available') && (
        <p
          role="alert"
          className="mt-space-md rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-space-md py-2 text-sm text-deep-river"
        >
          {t('Seat {id} was just taken by another traveler. Please choose another seat.', {
            id: seatCodes(seats.filter((s) => s.status !== 'available')),
          })}
        </p>
      )}

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-7">
          <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
            <div className="mb-space-md flex flex-wrap items-start justify-between gap-space-md">
              <div>
                <h2 className="flex items-center gap-2 font-headline-md text-headline-sm text-deep-river">
                  <span className="h-2 w-2 rounded-full bg-teal-flow" />
                  {seatMap.vesselName}
                </h2>
                <p className="text-xs text-on-surface-variant">
                  {t('{count} seats • live availability', { count: seatMap.seats.length })}
                </p>
              </div>
              <SeatLegend />
            </div>
            <SeatMapView
              seatMap={seatMap}
              selectedIds={seatIds}
              fareVnd={trip.fareVnd}
              onSelect={toggleSeat}
            />
          </div>
        </div>
        <div className="lg:col-span-5">
          <BookingSummary
            trip={trip}
            seats={seats}
            totals={totals}
            limitReached={seatLimitReached}
            canBook={seatMap.canBook !== false}
            onContinue={() => hold.submit(trip.id, totals.totalVnd, seatMap.journey)}
            busy={hold.busy}
            failure={hold.failure}
          />
        </div>
      </div>

    </div>
  )
}
