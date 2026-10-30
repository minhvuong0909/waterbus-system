import { useState } from 'react'
import { DataTable, ErrorState, PageHeader, PageLoader } from '../../components/ui'
import { getOperationTrips } from '../../features/admin/services/operationsService'
import type { AdminTrip } from '../../features/admin/types'
import { useDocumentTitle, useFetch } from '../../hooks'
import { formatNumber } from '../../utils/format'
import { getLowestFares } from '../../features/trips/services/fareService'
import { t } from '../../i18n'
import { formatDateTime } from '../../utils/time'
import type { TripStatus, TripType } from '../../features/trips/types'
import type { Column } from '../../components/ui'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

const STATUSES: TripStatus[] = [
  'Scheduled',
  'Boarding',
  'EnRoute',
  'Suspended',
  'Arrived',
  'Completed',
  'Cancelled',
  'Terminated',
]

export default function TripsPage() {
  useDocumentTitle('Trips')
  const trips = useFetch(getOperationTrips)
  // Lowest active fare of each trip type from `GET /fares` (BR-FARE-01: price depends on type and seat class only).
  const fares = useFetch<Partial<Record<TripType, number>>>(() =>
    getLowestFares().catch(() => ({})),
  )
  const [filter, setFilter] = useState<'all' | TripStatus>('all')

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  const rows = trips.data.filter((trip) => filter === 'all' || trip.status === filter)

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime) },
    { header: 'Route', render: (x) => x.routeName },
    { header: 'Boat', render: (x) => <span className="font-mono font-semibold">{x.boatCode ?? '—'}</span> },
    { header: 'Type', render: (x) => t(x.tripType === 'Commuter' ? 'Regular' : 'Sightseeing') },
    {
      header: 'Fares from (VND)',
      render: (x) => {
        const lowest = fares.data?.[x.tripType]
        return lowest === undefined ? '—' : formatNumber(lowest)
      },
      className: 'text-right',
    },
    { header: 'Status', render: (x) => <TripStatusBadge status={x.status} /> },
  ]

  return (
    <>
      <PageHeader
        title="Trips"
        description="Trips from 12 hours ago to 7 days ahead, with their boat and status."
        actions={
          <label className="flex items-center gap-2 text-sm text-on-surface-variant">
            {t('Status')}
            <select
              value={filter}
              onChange={(e) => setFilter(e.target.value as 'all' | TripStatus)}
              className="rounded-lg border border-outline-variant bg-white px-2 py-1.5 text-sm text-deep-river"
            >
              <option value="all">{t('All')}</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {t(s === 'EnRoute' ? 'In transit' : s)}
                </option>
              ))}
            </select>
          </label>
        }
      />
      <DataTable columns={columns} rows={rows} rowKey={(x) => x.id} empty="No trips match this filter." />
    </>
  )
}
