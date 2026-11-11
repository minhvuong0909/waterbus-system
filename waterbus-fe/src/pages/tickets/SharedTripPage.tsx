import { useParams } from 'react-router-dom'
import { Button, ErrorState, Icon, Notice, PageLoader } from '../../components/ui'
import SharedTripPassengers from '../../features/tickets/components/SharedTripPassengers'
import { useSharedTrip } from '../../features/tickets/hooks/useTickets'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'
import type { TripStatus } from '../../features/trips/types'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { isNotFound } from '../../services/apiClient'
import { formatClock, formatLongDate } from '../../utils/time'

function Fact({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div>
      <dt className="text-xs text-on-surface-variant">{label}</dt>
      <dd className={`font-semibold text-deep-river ${mono ? 'font-mono' : ''}`}>{value}</dd>
    </div>
  )
}

/**
 * Journey behind a Passenger TripAccess Link (BR-ID-05). Read only and open without an account:
 * it never shows the price, the payment, the buyer or the boarding QR (BR-ID-03, BR-NOT-02).
 */
export default function SharedTripPage() {
  useDocumentTitle('Shared journey')
  const { token = '' } = useParams()
  const { trip, loading, error, retry } = useSharedTrip(token)

  if (isNotFound(error)) {
    return (
      <ErrorState
        icon="lock"
        title={t('This link is invalid or has expired')}
        message={t('Ask the person who booked the trip to send you a new link.')}
      />
    )
  }
  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip) return <PageLoader />

  const stopped = trip.bookingStatus === 'Cancelled' || trip.bookingStatus === 'Terminated'

  return (
    <div className="mx-auto w-full max-w-3xl px-margin pb-space-3xl pt-space-lg">
      <p className="text-2xs font-bold uppercase tracking-wider text-teal-flow">
        {t('Shared journey')}
      </p>
      <div className="mt-1 flex flex-wrap items-center justify-between gap-space-sm">
        <h1 className="font-display text-4xl font-bold tracking-tight text-deep-river">
          {trip.routeName}
        </h1>
        <TripStatusBadge status={trip.tripStatus as TripStatus} />
      </div>

      {stopped && (
        <div className="mt-space-md">
          <Notice tone="error">
            {trip.bookingStatus === 'Cancelled'
              ? t('This booking was cancelled.')
              : t('This trip ended before reaching every stop.')}
          </Notice>
        </div>
      )}

      <section className="mt-space-lg rounded-2xl bg-white p-space-lg shadow-card">
        <ol className="relative space-y-space-md border-l-2 border-dashed border-outline-variant pl-space-lg">
          <li className="relative">
            <span className="absolute -left-[31px] top-1 h-3 w-3 rounded-full border-2 border-teal-flow bg-white" />
            <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
              {t('Board at')}
            </div>
            <div className="font-display text-2xl font-bold text-deep-river">
              {trip.boardingStation}
            </div>
          </li>
          <li className="relative">
            <span className="absolute -left-[31px] top-1 h-3 w-3 rounded-full bg-teal-flow" />
            <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
              {t('Get off at')}
            </div>
            <div className="font-display text-2xl font-bold text-deep-river">
              {trip.disembarkingStation}
            </div>
          </li>
        </ol>

        <dl className="mt-space-lg grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md sm:grid-cols-4">
          <Fact label={t('Date')} value={formatLongDate(trip.departureTime)} />
          <Fact label={t('Trip departs')} value={formatClock(new Date(trip.departureTime))} />
          <Fact label={t('Trip arrives')} value={formatClock(new Date(trip.arrivalTime))} />
          <Fact label={t('Booking code')} value={trip.bookingCode} mono />
        </dl>
      </section>

      <section className="mt-space-md rounded-2xl bg-white p-space-lg shadow-card">
        <h2 className="font-display text-lg font-bold text-deep-river">{t('Passengers')}</h2>
        <SharedTripPassengers passengers={trip.tickets} />
      </section>

      <div className="mt-space-lg flex flex-col items-start gap-space-sm sm:flex-row sm:items-center sm:justify-between">
        <p className="flex items-start gap-2 text-sm text-on-surface-variant">
          <Icon name="qr_code_2" className="mt-0.5 text-lg text-teal-flow" />
          {t('The boarding QR stays with the person who booked. Board together with them.')}
        </p>
        <Button to={`${ROUTES.liveTracking}?tripId=${trip.tripId}`} icon="near_me" className="shrink-0">
          {t('Track This Trip')}
        </Button>
      </div>
    </div>
  )
}
