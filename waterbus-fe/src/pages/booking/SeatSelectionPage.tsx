import { Breadcrumb, ErrorState, FeatureStrip, Icon, PageLoader } from '../../components/ui'
import SeatMapView, { SeatLegend } from '../../features/booking/components/SeatMapView'
import {
  BookingSummary,
  SeatRecommendation,
  TripStrip,
} from '../../features/booking/components/SeatSelectionParts'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { seatFeeFor } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

const CUES = [
  {
    icon: 'qr_code_scanner',
    title: 'QR E-Ticket Boarding',
    description: 'Tap and go at automated turnstiles across all river piers.',
  },
  {
    icon: 'radar',
    title: 'Real-Time Vessel Tracking',
    description: 'Track WB-01 hydrofoil speed and live catamaran dock ETA.',
  },
  {
    icon: 'accessible',
    title: '100% Step-Free Access',
    description: 'Universal low-incline gangways on all docks and catamarans.',
  },
]

export default function SeatSelectionPage() {
  useDocumentTitle('Choose Your Seat')
  const { loading, error, retry, trip, seatMap, seat, seatId, totals, passenger, chooseSeat } =
    useBooking()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !seatMap || !totals) return <PageLoader />

  const vipFeeVnd = seatFeeFor(
    seatMap.seats.find((s) => s.tier === 'vip'),
    trip.fareVnd,
  )

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
            {t('Step 2 of 4: Seat Selection')}
          </span>
          <h1 className="mt-1 font-headline-lg text-headline-lg tracking-tight text-deep-river">
            {t('Choose Your Seat')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {t('Select your preferred seat in the climate-controlled panoramic salon.')}
          </p>
        </div>
        <div className="flex items-center gap-space-md self-start rounded-xl bg-white px-space-md py-2 text-xs shadow-sm md:self-auto">
          <span className="flex items-center gap-1.5 text-on-surface-variant">
            <span className="h-2 w-2 rounded-full bg-signal-amber" />{' '}
            {t('Fast-filling morning service')}
          </span>
          <span className="text-outline">•</span>
          <span className="flex items-center gap-1 font-semibold text-teal-flow">
            <Icon name="timer" className="text-[15px]" /> {t('Cart holds for 10:00')}
          </span>
        </div>
      </div>

      <div className="mt-space-md space-y-space-md">
        <TripStrip trip={trip} seat={seat} />
        <div className="flex items-center justify-between gap-2 rounded-xl border border-teal-flow/20 bg-sand-light/60 px-space-md py-2 text-xs text-on-surface-variant">
          <span className="flex items-center gap-2">
            <Icon name="touch_app" className="text-[18px] text-teal-flow" />
            {t(
              'Tap an available seat to change your reservation. Seats are held for 10:00 during checkout.',
            )}
          </span>
          <span className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-wider text-teal-flow">
            <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" /> {t('Live Inventory')}
          </span>
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-7">
          <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
            <div className="mb-space-md flex flex-wrap items-start justify-between gap-space-md">
              <div>
                <h2 className="flex items-center gap-2 font-headline-md text-headline-sm text-deep-river">
                  <span className="h-2 w-2 rounded-full bg-teal-flow" />
                  {seatMap.deckName}
                </h2>
                <p className="text-xs text-on-surface-variant">{seatMap.vesselModel}</p>
              </div>
              <SeatLegend vipFeeVnd={vipFeeVnd} />
            </div>
            <SeatMapView
              seatMap={seatMap}
              selectedId={seatId}
              vipFeeVnd={vipFeeVnd}
              onSelect={chooseSeat}
            />
          </div>
          <SeatRecommendation />
        </div>
        <div className="lg:col-span-5">
          <BookingSummary
            trip={trip}
            seat={seat}
            seatId={seatId}
            totals={totals}
            category={passenger.category.charAt(0).toUpperCase() + passenger.category.slice(1)}
          />
        </div>
      </div>

      <FeatureStrip items={CUES} className="mt-space-xl" />
    </div>
  )
}
