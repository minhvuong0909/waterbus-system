import { useState } from 'react'
import { ErrorState, PageHeader, PageLoader, Panel, SelectField } from '../../components/ui'
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
  const [windows, setWindows] = useState<Record<string, CheckInWindow>>({})
  const stops = useFetch(() => (tripId ? getTripStops(tripId) : Promise.resolve([])), [tripId])

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  const upcoming = trips.data.filter((x) => !isFinished(x.status))
  const selectedTrip = upcoming.find((trip) => trip.id === tripId)
  const assignmentStops = stops.data?.filter((stop) => stop.canBoard && stop.status !== 'Departed' && stop.status !== 'Skipped')
    .map((stop) => ({ ...stop, ...windows[stop.callId] }))
  return (
    <>
      <PageHeader title="Check-in" description="When staff may check passengers in at each stop of a trip." />
      <Panel>
        <SelectField label="Trip" value={tripId} onChange={(e) => { setTripId(e.target.value); setWindows({}) }}>
          <option value="">{t('Choose a trip')}</option>
          {upcoming.map((x) => (
            <option key={x.id} value={x.id}>
              {formatDateTime(x.departureTime)} • {x.routeName} • {x.boatCode ?? '—'}
            </option>
          ))}
        </SelectField>
        {upcoming.length === 0 && (
          <p className="mt-3 text-sm text-on-surface-variant">{t('No trips are scheduled or running.')}</p>
        )}
      </Panel>

      {tripId && (
        <Panel title="Stops" className="mt-4">
          {stops.error ? (
            <ErrorState compact onRetry={stops.retry} />
          ) : stops.fetching || !stops.data ? (
            <PageLoader />
          ) : (
            <>
              <div className="divide-y divide-outline-variant/30">
                {stops.data.filter(isBoardingStop).map((stop) => (
                  <CheckInWindowRow key={stop.callId} stop={stop} onSaved={(window) =>
                    setWindows((current) => ({ ...current, [window.tripStopCallId]: window }))} />
                ))}
              </div>
              {stops.data.some((stop) => !isBoardingStop(stop)) && (
                <p className="mt-3 text-xs text-on-surface-variant">
                  {t('Stops where nobody boards (such as the last stop) have no check-in window.')}
                </p>
              )}
            </>
          )}
        </Panel>
      )}
      {selectedTrip && !stops.fetching && !stops.error && assignmentStops && (
        <ScannerAssignmentsPanel key={tripId} routeName={selectedTrip.routeName}
          stops={assignmentStops} />
      )}
    </>
  )
}
