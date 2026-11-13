import { useState } from 'react'
import { Button, DataTable, ErrorState, FilterBar, Modal, Notice, PageHeader, PageLoader, SelectField, TextField } from '../../components/ui'
import AdHocTripForm from '../../features/admin/components/AdHocTripForm'
import AssignBoatForm from '../../features/admin/components/AssignBoatForm'
import StatusBadge from '../../features/admin/components/StatusBadge'
import { getBoats } from '../../features/operations/services/fleetService'
import { getOperationTrips } from '../../features/operations/services/operationsService'
import { getRoutes } from '../../features/admin/services/routeService'
import type { AdminTrip } from '../../features/operations/types'
import { combineFetches, useDocumentTitle, useFetch, useReload } from '../../hooks'
import { formatNumber } from '../../utils/format'
import { getLowestFares } from '../../features/trips/services/fareService'
import { t } from '../../i18n'
import { formatDateTime, toVietnamDate } from '../../utils/time'
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

type Dialog = { kind: 'boat'; trip: AdminTrip } | { kind: 'trip' } | null

export default function TripsPage() {
  useDocumentTitle('Trips')
  const [version, reload] = useReload()
  // Empty: the default window (12 hours ago to 7 days ahead). A day: the 7 days from that day, Vietnam time.
  const [weekFrom, setWeekFrom] = useState('')
  const trips = useFetch(
    () => getOperationTrips(weekFrom ? { from: new Date(`${weekFrom}T00:00:00+07:00`).toISOString() } : undefined),
    [weekFrom, version],
  )
  // Chỉ tàu đang hoạt động: không gán được chuyến cho tàu đã ngừng hoạt động.
  const boats = useFetch(() => getBoats(true))
  const routes = useFetch(() => getRoutes(true))
  // Lowest active fare of each trip type from `GET /fares` (BR-FARE-01: price depends on type and seat class only).
  const fares = useFetch<Partial<Record<TripType, number>>>(() =>
    getLowestFares().catch(() => ({})),
  )
  const [filter, setFilter] = useState<'all' | TripStatus>('all')
  const [dialog, setDialog] = useState<Dialog>(null)
  const [movedSeats, setMovedSeats] = useState(0)

  const all = combineFetches(trips, boats, routes)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!trips.data || !boats.data || !routes.data) return <PageLoader />

  const rows = trips.data.filter((trip) => filter === 'all' || trip.status === filter)

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime), className: 'whitespace-nowrap' },
    { header: 'Route', render: (x) => x.routeName },
    {
      header: 'Boat',
      render: (x) =>
        x.boatCode ? (
          <span className="font-mono font-semibold">{x.boatCode}</span>
        ) : x.status === 'Scheduled' ? (
          // Without a boat the trip is not on sale (BR-SCH-04).
          <StatusBadge tone="upcoming" label="No boat" />
        ) : (
          '—'
        ),
    },
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
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) =>
        // The boat can only change while the trip has not started boarding.
        x.status === 'Scheduled' && (
          <Button
            size="sm"
            variant={x.boatId ? 'neutral' : 'secondary'}
            onClick={() => {
              setMovedSeats(0)
              setDialog({ kind: 'boat', trip: x })
            }}
          >
            {x.boatId ? t('Change boat') : t('Assign boat')}
          </Button>
        ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Trips"
        description="Seven days of trips with their boat and status. A trip is on sale once it has a boat."
        actions={
          <Button
            size="sm"
            icon="add"
            disabled={routes.data.length === 0}
            onClick={() => setDialog({ kind: 'trip' })}
          >
            {t('Add trip')}
          </Button>
        }
      />
      {movedSeats > 0 && (
        <div className="mb-3">
          <Notice tone="success">
            {t('Boat changed. {count} sold seats were moved to the new boat.', { count: movedSeats })}
          </Notice>
        </div>
      )}
      <FilterBar onReset={weekFrom !== '' || filter !== 'all' ? () => { setWeekFrom(''); setFilter('all') } : undefined}>
        <TextField label="From" type="date" value={weekFrom} onChange={(e) => setWeekFrom(e.target.value)} />
        <SelectField label="Status" value={filter} onChange={(e) => setFilter(e.target.value as 'all' | TripStatus)}>
          <option value="all">{t('All')}</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {t(s === 'EnRoute' ? 'In transit' : s)}
            </option>
          ))}
        </SelectField>
      </FilterBar>
      <DataTable paginate columns={columns} rows={rows} rowKey={(x) => x.id} empty="No trips match this filter." />

      {dialog?.kind === 'boat' && (
        <Modal title={dialog.trip.boatId ? 'Change boat' : 'Assign boat'} onClose={() => setDialog(null)}>
          <AssignBoatForm
            trip={dialog.trip}
            boats={boats.data}
            onDone={(result) => {
              setMovedSeats(result.remappedReservations)
              setDialog(null)
              reload()
            }}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'trip' && (
        <Modal title="Add trip" onClose={() => setDialog(null)}>
          <AdHocTripForm
            routes={routes.data}
            onDone={(departureTime) => {
              // Show the week of the new trip, so its boat can be assigned right away.
              setWeekFrom(toVietnamDate(new Date(departureTime)))
              setFilter('all')
              setDialog(null)
              reload()
            }}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
    </>
  )
}
