import { useState } from 'react'
import { Button, DataTable, ErrorState, Modal, Notice, PageHeader, PageLoader } from '../../components/ui'
import type { Column } from '../../components/ui'
import ActiveToggle from '../../features/admin/components/ActiveToggle'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import RouteForm from '../../features/admin/components/RouteForm'
import { ActiveBadge } from '../../features/admin/components/StatusBadge'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import { getRoute, getRoutes, setRouteActive } from '../../features/admin/services/routeService'
import { getStations } from '../../features/admin/services/stationService'
import type { RouteDetail, RouteListItem } from '../../features/admin/types'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatNumber } from '../../utils/format'

type Dialog =
  | { kind: 'form'; route?: RouteDetail }
  | { kind: 'deactivate'; route: RouteListItem }
  | null

/** Routes: the ordered stations a boat calls at. Schedules and trips are built on a route. */
export default function RoutesPage() {
  useDocumentTitle('Routes')
  const [version, reload] = useReload()
  const routes = useFetch(() => getRoutes(), [version])
  const stations = useFetch(getStations)
  const [dialog, setDialog] = useState<Dialog>(null)
  const action = useSubmit()

  const all = combineFetches(routes, stations)
  if (all.error) return <ErrorState onRetry={all.retry} />
  if (!routes.data || !stations.data) return <PageLoader />

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  // The list has no stops, so the editor opens on the full route.
  const edit = async (id: string) => {
    const route = await action.run(() => getRoute(id))
    if (route) setDialog({ kind: 'form', route })
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
    { header: 'Stops', render: (r) => r.stopCount, className: 'text-right' },
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
          <Button size="sm" variant="neutral" icon="edit" disabled={action.busy} onClick={() => edit(r.id)}>
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
      <DataTable columns={columns} rows={routes.data} rowKey={(r) => r.id} empty="No routes yet." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.route ? 'Edit route' : 'Add route'} onClose={() => setDialog(null)}>
          <RouteForm
            route={dialog.route}
            stations={stations.data}
            onDone={closeAndReload}
            onCancel={() => setDialog(null)}
          />
        </Modal>
      )}
      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate route"
          message={t('Deactivate {name}? No new schedule or trip can use this route.', {
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
