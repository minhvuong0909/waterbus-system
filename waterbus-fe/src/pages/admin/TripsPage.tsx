import { useState } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  ActionButton,
  DataTable,
  Modal,
  Notice,
  PageHeader,
  SelectField,
  StatusBadge,
} from '../../features/admin/components/AdminUi'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import { assignBoat, getBoats, getRoutes, getTrips } from '../../features/admin/services/adminService'
import type { AdminBoat, AdminTrip, AdminTripStatus, AdminTripType } from '../../features/admin/types'
import { formatDateTime } from '../../features/admin/utils'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { formatNumber } from '../../utils/format'
import { getLowestFares } from '../../features/trips/services/fareService'
import { t } from '../../i18n'
import { USE_MOCK } from '../../constant/env'

const CAN_REASSIGN: AdminTripStatus[] = ['Scheduled', 'Boarding', 'Suspended']
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

function AssignBoatForm({ trip, boats, onCancel, onDone }: { trip: AdminTrip; boats: AdminBoat[]; onCancel: () => void; onDone: () => void }) {
  const [boatId, setBoatId] = useState(trip.boatId)
  const { run, busy, error } = useSubmit()

  return (
    <form
      noValidate
      className="space-y-3"
      onSubmit={async (e) => {
        e.preventDefault()
        const saved = await run(() => assignBoat(trip.id, boatId))
        if (saved) onDone()
      }}
    >
      <p className="text-sm text-on-surface-variant">
        {t('Trip of {time}. A boat cannot be on two trips at the same time.', { time: formatDateTime(trip.departureTime) })}
      </p>
      <SelectField label="Boat" value={boatId} onChange={(e) => setBoatId(e.target.value)}>
        {boats.map((b) => (
          <option key={b.id} value={b.id}>
            {b.code} • {b.name}
          </option>
        ))}
      </SelectField>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Cancel')}
        </ActionButton>
        <ActionButton type="submit" disabled={busy || boatId === trip.boatId}>
          {busy ? t('Saving…') : t('Assign boat')}
        </ActionButton>
      </div>
    </form>
  )
}

export default function TripsPage() {
  useDocumentTitle('Trips')
  const [version, reload] = useReload()
  const trips = useFetch(getTrips, [version])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  // Real mode: lowest active fare of each trip type from `GET /fares` (BR-FARE-01: price depends on type and seat class only).
  const fares = useFetch<Partial<Record<AdminTripType, number>>>(() =>
    USE_MOCK ? Promise.resolve({}) : getLowestFares().catch(() => ({})),
  )
  const [filter, setFilter] = useState<'all' | AdminTripStatus>('all')
  const [assigning, setAssigning] = useState<AdminTrip | null>(null)
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
        if (USE_MOCK) return formatNumber(x.basePrice)
        const lowest = fares.data?.[x.tripType]
        return lowest === undefined ? '—' : formatNumber(lowest)
      },
      className: 'text-right',
    },
    { header: 'Status', render: (x) => <StatusBadge status={x.status} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) =>
        USE_MOCK && CAN_REASSIGN.includes(x.status) ? (
          <ActionButton tone="secondary" icon="swap_horiz" onClick={() => setAssigning(x)}>
            {t('Change boat')}
          </ActionButton>
        ) : null,
    },
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

      {assigning && (
        <Modal title="Assign boat" onClose={() => setAssigning(null)}>
          <AssignBoatForm
            trip={assigning}
            boats={boats.data}
            onCancel={() => setAssigning(null)}
            onDone={() => {
              setAssigning(null)
              reload()
            }}
          />
        </Modal>
      )}
    </>
  )
}
