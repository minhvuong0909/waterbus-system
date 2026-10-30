import { useState } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  DataTable,
  PageHeader,
  StatusBadge,
} from '../../features/admin/components/AdminUi'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload } from '../../features/admin/hooks/useAdmin'
import { getBoats, getRoutes, getTrips } from '../../features/admin/services/adminService'
import type { AdminTrip, AdminTripStatus, AdminTripType } from '../../features/admin/types'
import { formatDateTime } from '../../features/admin/utils'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { formatNumber } from '../../utils/format'
import { getLowestFares } from '../../features/trips/services/fareService'
import { t } from '../../i18n'

const STATUSES: AdminTripStatus[] = [
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
  const [version] = useReload()
  const trips = useFetch(getTrips, [version])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  // Lowest active fare of each trip type from `GET /fares` (BR-FARE-01: price depends on type and seat class only).
  const fares = useFetch<Partial<Record<AdminTripType, number>>>(() =>
    getLowestFares().catch(() => ({})),
  )
  const [filter, setFilter] = useState<'all' | AdminTripStatus>('all')
  const state = combineFetches(trips, routes, boats)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !routes.data || !boats.data) return <PageLoader />

  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'
  const boatCode = (id: string) => boats.data?.find((b) => b.id === id)?.code ?? '—'
  const rows = trips.data.filter((trip) => filter === 'all' || trip.status === filter)

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime) },
    { header: 'Route', render: (x) => x.routeName ?? routeName(x.routeId) },
    { header: 'Boat', render: (x) => <span className="font-mono font-semibold">{boatCode(x.boatId)}</span> },
    { header: 'Type', render: (x) => t(x.tripType === 'Commuter' ? 'Regular' : 'Sightseeing') },
    {
      header: 'Fares from (VND)',
      render: (x) => {
        const lowest = fares.data?.[x.tripType]
        return lowest === undefined ? '—' : formatNumber(lowest)
      },
      className: 'text-right',
    },
    { header: 'Status', render: (x) => <StatusBadge status={x.status} /> },
  ]

  return (
    <>
      <PageHeader
        title="Trips"
        description="Real runs of the boats, generated from the schedules."
        actions={
          <label className="flex items-center gap-2 text-sm text-on-surface-variant">
            {t('Status')}
            <select
              value={filter}
              onChange={(e) => setFilter(e.target.value as 'all' | AdminTripStatus)}
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
