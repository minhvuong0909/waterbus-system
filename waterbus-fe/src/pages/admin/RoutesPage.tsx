import { useState } from 'react'
import { Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, Notice, PageHeader, PageLoader, TextField } from '../../components/ui'
import { INSET_CLASS } from '../../components/ui/styles'
import type { Column } from '../../components/ui'
import ActiveToggle from '../../features/admin/components/ActiveToggle'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import RouteForm from '../../features/admin/components/RouteForm'
import { ActiveBadge } from '../../features/admin/components/StatusBadge'
import { getRoute, getRoutes, setRouteActive } from '../../features/admin/services/routeService'
import { getStations } from '../../features/admin/services/stationService'
import type { RouteDetail, RouteListItem } from '../../features/admin/types'
import { combineFetches, useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatNumber } from '../../utils/format'

type Dialog =
  | { kind: 'form'; route?: RouteDetail }
  | { kind: 'stops'; route: RouteDetail }
  | { kind: 'deactivate'; route: RouteListItem }
  | null

/** Tuyến: các bến theo thứ tự tàu ghé. Lịch chạy và chuyến đều dựa trên một tuyến. */
export default function RoutesPage() {
  useDocumentTitle('Routes')
  const [version, reload] = useReload()
  const routes = useFetch(() => getRoutes(), [version])
  const stations = useFetch(getStations)
  const [dialog, setDialog] = useState<Dialog>(null)
  const [search, setSearch] = useState('')
  const [serviceType, setServiceType] = useState('')
  const [status, setStatus] = useState('')
  const action = useSubmit()

  const all = combineFetches(routes, stations)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!routes.data || !stations.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  const needle = search.trim().toLowerCase()
  const filteredRoutes = routes.data.filter(
    (r) =>
      (needle === '' || r.code.toLowerCase().includes(needle) || r.name.toLowerCase().includes(needle)) &&
      (serviceType === '' || r.serviceType === serviceType) &&
      (status === '' || (status === 'active') === r.isActive),
  )

  // Danh sách không kèm bến dừng nên trình sửa mở trên tuyến đầy đủ.
  const open = async (id: string, kind: 'form' | 'stops') => {
    const route = await action.run(() => getRoute(id))
    if (route) setDialog({ kind, route })
  }

  const columns: Column<RouteListItem>[] = [
    { header: 'Code', render: (r) => <span className="font-mono font-semibold">{r.code}</span> },
    {
      header: 'Route',
      className: 'min-w-[13rem]',
      render: (r) => (
        <>
          <div className="font-semibold">{r.name}</div>
          <div className="text-xs text-on-surface-variant">
            {r.departureStationName} → {r.arrivalStationName}
          </div>
        </>
      ),
    },
    { header: 'Service', render: (r) => t(r.serviceType) },
    {
      header: 'Stops',
      className: 'text-right',
      render: (r) => (
        <button
          type="button"
          disabled={action.busy}
          aria-label={t('View stops of {name}', { name: r.name })}
          title={t('View stops')}
          onClick={() => open(r.id, 'stops')}
          className="font-semibold text-teal-flow underline-offset-2 hover:underline"
        >
          {r.stopCount}
        </button>
      ),
    },
    {
      header: 'Duration',
      render: (r) => t('{minutes} min', { minutes: r.estimatedDurationMinutes }),
      className: 'text-right',
    },
    { header: 'Distance', render: (r) => `${formatNumber(r.distanceKm)} km`, className: 'whitespace-nowrap text-right' },
    { header: 'Status', render: (r) => <ActiveBadge active={r.isActive} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (r) => (
        <span className="inline-flex gap-1">
          <Button size="sm" variant="neutral" icon="edit" disabled={action.busy} onClick={() => open(r.id, 'form')}>
            {t('Edit')}
          </Button>
          <ActiveToggle
            active={r.isActive}
            busy={action.busy}
            onDeactivate={() => setDialog({ kind: 'deactivate', route: r })}
            onActivate={async () => {
              if (await action.run(() => setRouteActive(r.id, true).then(() => true))) reload()
            }}
          />
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Routes"
        description="The stations a boat calls at, in order. Schedules and trips are built on a route."
        actions={
          <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add route')}
          </Button>
        }
      />
      {action.error && (
        <div className="mb-3">
          <Notice tone="error">{action.error}</Notice>
        </div>
      )}
      <FilterBar>
        <TextField
          label="Search"
          placeholder={t('Search by name or code')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <FilterChips label="Service" value={serviceType} onChange={setServiceType} options={[
          { value: '', label: 'All types' }, { value: 'Regular', label: 'Regular' }, { value: 'Sightseeing', label: 'Sightseeing' },
        ]} />
        <FilterChips label="Status" value={status} onChange={setStatus} options={[
          { value: '', label: 'All statuses' }, { value: 'active', label: 'Active only' }, { value: 'inactive', label: 'Inactive only' },
        ]} />
      </FilterBar>
      <DataTable paginate columns={columns} rows={filteredRoutes} rowKey={(r) => r.id} empty="No routes match this filter." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.route ? 'Edit route' : 'Add route'} onClose={() => setDialog(null)}>
          <RouteForm
            route={dialog.route}
            others={routes.data.filter((r) => r.id !== dialog.route?.id)}
            stations={stations.data}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'stops' && (
        <Modal title="Route stops" onClose={() => setDialog(null)}>
          <p className="mb-3 text-sm text-on-surface-variant">{dialog.route.name}</p>
          <ol className={`${INSET_CLASS} divide-y divide-outline-variant/30`}>
            {dialog.route.stops.map((stop) => (
              <li key={stop.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-mist text-xs font-bold text-deep-river">
                  {stop.sequenceNo}
                </span>
                <span className="min-w-0 flex-1 font-semibold text-deep-river">{stop.stationName}</span>
              </li>
            ))}
          </ol>
        </Modal>
      )}
      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate route"
          message={t('Deactivate {name}? No new schedule or trip can use this route. This is refused while an active schedule or an unfinished trip still uses it.', {
            name: dialog.route.name,
          })}
          confirmLabel="Deactivate"
          action={() => setRouteActive(dialog.route.id, false)}
          onDone={closeAndReload}
          onClose={() => setDialog(null)}
        />
      )}
    </>
  )
}
