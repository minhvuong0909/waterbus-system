import { Link, useSearchParams } from 'react-router-dom'
import FleetMap from '../../components/map/FleetMap'
import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import { isGuid } from '../../features/booking/utils'
import StopTimeline from '../../features/tracking/components/StopTimeline'
import { useTripTracking } from '../../features/tracking/hooks/useTripTracking'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import { formatClock } from '../../utils/time'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

/** Opened from a ticket (`?tripId=`): no trip, nothing to follow. */
function NoTripChosen() {
  return (
    <div className="mx-auto flex min-h-[50vh] max-w-lg flex-col items-center justify-center gap-4 px-margin py-space-3xl text-center">
      <Icon name="directions_boat" className="text-4xl text-teal-flow" />
      <h1 className="text-2xl font-bold text-deep-river">{t('Open tracking from your ticket')}</h1>
      <p className="text-sm text-on-surface-variant">
        {t('Open one of your tickets and choose to follow its trip.')}
      </p>
      <Link to={ROUTES.myTickets} className="rounded-full bg-teal-flow px-6 py-2.5 text-sm font-semibold text-on-primary">
        {t('My Tickets')}
      </Link>
    </div>
  )
}

function TripTracking({ tripId }: { tripId: string }) {
  const { data: trip, error, retry, position, signedIn } = useTripTracking(tripId)

  if (error) return <ErrorState onRetry={retry} />
  if (!trip) return <PageLoader />

  const boats = position && trip.boat
    ? [{ tripId: trip.tripId, code: trip.boat.code, latitude: position.latitude, longitude: position.longitude }]
    : []
  const positionNote = !signedIn
    ? t('Sign in to see where the boat is right now.')
    : position
      ? t('Position at {time}', { time: formatClock(new Date(position.observedAt)) })
      : t('Waiting for the boat to report its position.')

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb items={[{ label: 'My Tickets', to: ROUTES.myTickets }, { label: 'Live Trip' }]} />

      <div className="mt-space-md flex flex-wrap items-center gap-3">
        <h1 className="text-3xl font-bold tracking-tight text-deep-river">{trip.routeName}</h1>
        <TripStatusBadge status={trip.status} />
      </div>
      {trip.boat && (
        <p className="mt-1 text-sm text-on-surface-variant">
          {trip.boat.name} • {trip.boat.code}
        </p>
      )}

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-8">
          <div className="h-[60vh] min-h-[24rem]">
            <FleetMap
              stations={trip.stops.map((s) => ({ id: s.callId, name: s.stationName, latitude: s.latitude, longitude: s.longitude }))}
              boats={boats}
            />
          </div>
          <p className="mt-2 text-xs text-on-surface-variant">{positionNote}</p>
        </div>
        <section className="rounded-2xl border border-outline-variant/50 bg-white p-5 lg:col-span-4">
          <h2 className="mb-4 text-base font-bold text-deep-river">{t('Stops')}</h2>
          <StopTimeline stops={trip.stops} />
        </section>
      </div>
    </div>
  )
}

export default function LiveTrackingPage() {
  useDocumentTitle('Live Trip Tracking')
  const tripId = useSearchParams()[0].get('tripId')
  return tripId && isGuid(tripId) ? <TripTracking tripId={tripId} /> : <NoTripChosen />
}
