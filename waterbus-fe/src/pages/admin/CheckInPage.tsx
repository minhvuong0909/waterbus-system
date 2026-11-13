import { useState } from 'react'
import { ErrorState, PageHeader, PageLoader, Panel, SearchSelect } from '../../components/ui'
import ScannerAssignmentsPanel from '../../features/admin/components/ScannerAssignmentsPanel'
import CheckInWindowRow from '../../features/admin/components/CheckInWindowRow'
import type { CheckInWindow } from '../../features/admin/services/checkInService'
import { getTripStops } from '../../features/admin/services/checkInService'
import { getOperationTrips } from '../../features/operations/services/operationsService'
import { isBoardingStop, isFinished } from '../../features/operations/utils'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatDateTime } from '../../utils/time'

/** Admin sets, per stop of a trip, when staff may check passengers in (BR-BOARD-05). */
export default function CheckInPage() {
  useDocumentTitle('Check-in')
  const trips = useFetch(getOperationTrips)
  const [tripId, setTripId] = useState('')

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  const upcoming = trips.data.filter((x) => !isFinished(x.status))
  const selectedTrip = upcoming.find((trip) => trip.id === tripId)
  return (
    <>
      <PageHeader title="Check-in" description="When staff may check passengers in at each stop of a trip." />
      <Panel>
        <SearchSelect label="Trip" value={tripId} onChange={setTripId} placeholder="Choose a trip"
          hint="Search by route, boat or departure time."
          options={upcoming.map((trip) => ({ value: trip.id, label: trip.routeName,
            description: formatDateTime(trip.departureTime), badge: trip.boatCode ?? '—',
          }))} />
        {upcoming.length === 0 && (
          <p className="mt-3 text-sm text-on-surface-variant">{t('No trips are scheduled or running.')}</p>
        )}
      </Panel>

      {selectedTrip && <TripCheckIn key={selectedTrip.id} tripId={selectedTrip.id} routeName={selectedTrip.routeName} />}
    </>
  )
}

function TripCheckIn({ tripId, routeName }: { tripId: string; routeName: string }) {
  const [windows, setWindows] = useState<Record<string, CheckInWindow>>({})
  const stops = useFetch(() => getTripStops(tripId))
  if (stops.error) {
    return (
      <Panel title="Stops" className="mt-4">
        <ErrorState compact onRetry={stops.retry} />
      </Panel>
    )
  }
  if (stops.fetching || !stops.data) {
    return <Panel title="Stops" className="mt-4"><PageLoader /></Panel>
  }

  const boardingStops = stops.data.filter(isBoardingStop)
  const assignmentStops = stops.data
    .filter((stop) => stop.canBoard && stop.status !== 'Departed' && stop.status !== 'Skipped')
    .map((stop) => ({ ...stop, ...windows[stop.callId] }))

  return (
    <>
      <Panel title="Stops" className="mt-4">
        <div className="divide-y divide-outline-variant/30">
          {boardingStops.map((stop) => (
            <CheckInWindowRow key={stop.callId} stop={stop} onSaved={(window) =>
              setWindows((current) => ({ ...current, [window.tripStopCallId]: window }))} />
          ))}
        </div>
        {boardingStops.length !== stops.data.length && (
          <p className="mt-3 text-xs text-on-surface-variant">
            {t('Stops where nobody boards (such as the last stop) have no check-in window.')}
          </p>
        )}
      </Panel>
      <ScannerAssignmentsPanel routeName={routeName} stops={assignmentStops} />
    </>
  )
}
