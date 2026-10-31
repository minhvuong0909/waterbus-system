import { useState } from 'react'
import { ErrorState, PageHeader, PageLoader, Panel, SelectField } from '../../components/ui'
import CheckInWindowRow from '../../features/admin/components/CheckInWindowRow'
import { getTripStops } from '../../features/admin/services/checkInService'
import { getOperationTrips } from '../../features/admin/services/operationsService'
import { isFinished } from '../../features/admin/utils'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatDateTime } from '../../utils/time'

/** Admin sets, per stop of a trip, when staff may check passengers in (BR-BOARD-05). */
export default function CheckInPage() {
  useDocumentTitle('Check-in')
  const trips = useFetch(getOperationTrips)
  const [tripId, setTripId] = useState('')
  const stops = useFetch(() => (tripId ? getTripStops(tripId) : Promise.resolve([])), [tripId])

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  const upcoming = trips.data.filter((x) => !isFinished(x.status))
  return (
    <>
      <PageHeader title="Check-in" description="When staff may check passengers in at each stop of a trip." />
      <Panel>
        <SelectField label="Trip" value={tripId} onChange={(e) => setTripId(e.target.value)}>
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
          ) : !stops.data ? (
            <PageLoader />
          ) : (
            <div className="divide-y divide-outline-variant/30">
              {stops.data.map((stop) => (
                <CheckInWindowRow key={stop.callId} stop={stop} />
              ))}
            </div>
          )}
        </Panel>
      )}
    </>
  )
}
