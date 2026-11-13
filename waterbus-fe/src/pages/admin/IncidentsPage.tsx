import { useState } from 'react'
import { Button, DataTable, ErrorState, FilterChips, Modal, Notice, PageHeader, PageLoader, Pagination } from '../../components/ui'
import AlertCard from '../../features/admin/components/AlertCard'
import AlertsEmpty from '../../features/admin/components/AlertsEmpty'
import { pendingCountOf, sortAlerts } from '../../features/admin/utils/alertUtils'
import type { WeatherAlert } from '../../features/operations/types'
import {
  INCIDENT_KINDS,
  getOpenIncidents,
  getOperationLogs,
  getOperationTrips,
  resolveIncident,
} from '../../features/operations/services/operationsService'
import type { AdminTrip, OperationType } from '../../features/operations/types'
import { DISPATCH_OPERATIONS, OPERATION_LABEL, isFinished } from '../../features/operations/utils'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { formatDateTime } from '../../utils/time'
import { combineFetches, useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import OperationForm from '../../features/admin/components/OperationForm'
import type { Column } from '../../components/ui'
import CurrentWeatherPanel from '../../features/admin/components/CurrentWeatherPanel'
import { useAutoRefresh } from '../../features/admin/hooks/useAutoRefresh'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

const ALERTS_PAGE_SIZE = 10

export default function IncidentsPage() {
  useDocumentTitle('Incidents')
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const logs = useFetch(getOperationLogs, [version])
  const [alertVersion, reloadAlerts] = useReload()
  const [kindFilter, setKindFilter] = useState('')
  const [alertsPage, setAlertsPage] = useState(1)
  // Luôn tải HẾT mọi loại sự cố mở (không lọc ở BE): `awaitingDecision`/`waitingTrips` bên dưới phải
  // tính trên TOÀN BỘ sự cố đang mở, không phụ thuộc bộ lọc hiển thị - nếu lọc ở BE, 1 sự cố loại
  // khác đang chờ quyết định sẽ bị ẩn mất nút hành động khỏi "Trips in operation" một cách sai lệch.
  // Lọc theo loại chỉ áp dụng cho phần HIỂN THỊ card bên dưới, ở client.
  const alerts = useFetch(() => getOpenIncidents(true), [alertVersion])
  const [pending, setPending] = useState<{ trip: AdminTrip; type: OperationType; incidentId?: string } | null>(null)
  const closing = useSubmit()
  const [confirmClose, setConfirmClose] = useState<WeatherAlert | null>(null)
  // New alerts, emergencies and every status change (by any admin or a captain) show up without reloading.
  const refresh = () => {
    reload()
    reloadAlerts()
  }
  const connection = useFleetHub(
    { onAlert: reloadAlerts, onIncident: reloadAlerts, onTripStatus: refresh, onReconnected: refresh },
  )
  useAutoRefresh(refresh, trips.fetching || logs.fetching || alerts.fetching)
  const state = combineFetches(trips, logs, alerts)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !logs.data || !alerts.data) return <PageLoader />

  const tripLabel = (id: string | null) => {
    const trip = trips.data?.find((x) => x.id === id)
    return trip ? `${trip.boatCode ?? '—'} • ${formatDateTime(trip.departureTime)}` : '—'
  }
  const active = trips.data.filter((x) => !isFinished(x.status))
  const openAlerts = sortAlerts(alerts.data)
  const waitingTrips = openAlerts.reduce((sum, alert) => sum + pendingCountOf(alert), 0)

  const filteredAlerts = kindFilter ? openAlerts.filter((a) => a.kind === kindFilter) : openAlerts
  const alertsTotalPages = Math.max(1, Math.ceil(filteredAlerts.length / ALERTS_PAGE_SIZE))
  const alertsPageClamped = Math.min(alertsPage, alertsTotalPages)
  const pagedAlerts = filteredAlerts.slice(
    (alertsPageClamped - 1) * ALERTS_PAGE_SIZE,
    alertsPageClamped * ALERTS_PAGE_SIZE,
  )
  const chooseKindFilter = (value: string) => {
    setKindFilter(value)
    setAlertsPage(1)
  }

  // Trips an open alert affects and the admin has not decided about yet: tripId -> alert id.
  const awaitingDecision = new Map<string, string>()
  alerts.data.forEach((alert) =>
    alert.trips?.forEach((link) => {
      if (link.decision === null && !awaitingDecision.has(link.tripId)) awaitingDecision.set(link.tripId, alert.id)
    }),
  )
  // Only these answer an alert; Resume and Close trip are routine operations of the trip itself.
  const ANSWERS_ALERT: OperationType[] = ['Suspend', 'Cancel', 'Terminate', 'NoAction']

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime) },
    { header: 'Route', render: (x) => x.routeName },
    { header: 'Boat', render: (x) => <span className="font-mono font-semibold">{x.boatCode ?? '—'}</span> },
    {
      header: 'Status',
      render: (x) => (
        <span className="inline-flex flex-wrap items-center gap-2">
          <TripStatusBadge status={x.status} />
          {awaitingDecision.has(x.id) && (
            <span className="rounded-full bg-signal-amber/25 px-2 py-0.5 text-2xs font-semibold text-deep-river">
              {t('Needs your decision')}
            </span>
          )}
        </span>
      ),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) => {
        const incidentId = awaitingDecision.get(x.id)
        const operations: OperationType[] = [
          ...DISPATCH_OPERATIONS.filter((op) => x.allowedOperations.includes(op)),
          ...(incidentId ? (['NoAction'] as OperationType[]) : []),
        ]
        return (
          <span className="inline-flex flex-wrap justify-end gap-1">
            {operations.map((op) => (
              <Button size="sm"
                key={op}
                variant={op === 'Cancel' || op === 'Terminate' ? 'danger' : 'neutral'}
                onClick={() =>
                  setPending({ trip: x, type: op, incidentId: ANSWERS_ALERT.includes(op) ? incidentId : undefined })
                }
              >
                {t(OPERATION_LABEL[op])}
              </Button>
            ))}
          </span>
        )
      },
    },
  ]

  const logColumns: Column<(typeof logs.data)[number]>[] = [
    { header: 'Time', render: (l) => formatDateTime(l.at) },
    {
      header: 'Operation',
      render: (l) => (
        <span className={l.type === 'SOS' ? 'font-bold text-coral-glow' : 'font-semibold'}>{t(OPERATION_LABEL[l.type])}</span>
      ),
    },
    { header: 'Trip', render: (l) => tripLabel(l.tripId) },
    { header: 'Reason', render: (l) => l.reason },
    { header: 'By', render: (l) => l.by },
  ]

  return (
    <>
      <PageHeader title="Incidents" description="Weather warnings, and the controls to hold, cancel or end a trip." />

      <div className="mb-5 grid gap-3 sm:grid-cols-3">
        <div className="rounded-2xl border border-outline-variant/40 bg-white p-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-on-surface-variant">{t('Open alerts')}</div>
          <div className="mt-1 font-display text-2xl font-bold text-deep-river">{openAlerts.length}</div>
        </div>
        <div
          className={`rounded-2xl border p-4 ${
            waitingTrips > 0 ? 'border-coral-glow/40 bg-coral-glow/5' : 'border-outline-variant/40 bg-white'
          }`}
        >
          <div className="text-xs font-semibold uppercase tracking-wide text-on-surface-variant">
            {t('Trips waiting for a decision')}
          </div>
          <div
            className={`mt-1 font-display text-2xl font-bold ${
              waitingTrips > 0 ? 'text-coral-glow' : 'text-deep-river'
            }`}
          >
            {waitingTrips}
          </div>
        </div>
        <div className="flex flex-col justify-center gap-2 rounded-2xl border border-teal-flow/20 bg-teal-flow/5 p-4" role="status">
          <span className="text-sm font-semibold text-teal-flow">{connection === 'live' ? t('Live alerts connected') : t('Reconnecting to live alerts')}</span>
          <p className="text-xs leading-relaxed text-on-surface-variant">{t('Weather is monitored automatically. Alerts appear here without a manual check.')}</p>
        </div>
      </div>
      <div className="mb-6"><CurrentWeatherPanel /></div>
      {closing.error && (
        <div className="mb-3">
          <Notice tone="error">{closing.error}</Notice>
        </div>
      )}

      <section className="mb-8 space-y-3" aria-label={t('Open alerts')}>
        {openAlerts.length > 0 && (
          <FilterChips label="Incident type" value={kindFilter} onChange={chooseKindFilter} options={[
            { value: '', label: 'All types' }, ...INCIDENT_KINDS.map((kind) => ({ value: kind, label: kind })),
          ]} />
        )}
        {openAlerts.length === 0 ? (
          <AlertsEmpty />
        ) : filteredAlerts.length === 0 ? (
          <Notice tone="success">{t('No open incidents match this filter.')}</Notice>
        ) : (
          pagedAlerts.map((alert) => (
            <AlertCard
              key={alert.id}
              alert={alert}
              trips={trips.data ?? []}
              closing={closing.busy}
              onClose={setConfirmClose}
              onDecide={(trip, type, incidentId) => setPending({ trip, type, incidentId })}
            />
          ))
        )}
        <Pagination pageNumber={alertsPageClamped} totalPages={alertsTotalPages} onChange={setAlertsPage} />
      </section>

      <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Trips in operation')}</h2>
      <DataTable paginate columns={columns} rows={active} rowKey={(x) => x.id} empty="No trips are scheduled or running." />

      <h2 className="mb-2 mt-8 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Operation log')}</h2>
      <DataTable paginate columns={logColumns} rows={logs.data} rowKey={(l) => l.id} empty="Nothing has been done yet." />

      {confirmClose && (
        <Modal title={t('Close this alert?')} onClose={() => setConfirmClose(null)}>
          <p className="text-sm text-on-surface-variant">
            {t('Closing only ends the alert: no trip changes, and every trip keeps its current status.')}
          </p>
          {pendingCountOf(confirmClose) > 0 && (
            <p className="mt-2 rounded-lg bg-signal-amber/15 px-3 py-2 text-sm text-deep-river">
              {t('{count} trips still have no decision.', { count: pendingCountOf(confirmClose) })}
            </p>
          )}
          <div className="mt-4 flex justify-end gap-2">
            <Button size="sm" variant="neutral" onClick={() => setConfirmClose(null)}>
              {t('Back')}
            </Button>
            <Button size="sm"
              disabled={closing.busy}
              onClick={async () => {
                const alert = confirmClose
                await closing.run(() => resolveIncident(alert.id))
                setConfirmClose(null)
                reloadAlerts()
              }}
            >
              {closing.busy ? t('Working…') : t('Close alert')}
            </Button>
          </div>
        </Modal>
      )}

      {pending && (
        <Modal title={OPERATION_LABEL[pending.type]} onClose={() => setPending(null)}>
          <OperationForm
            trip={pending.trip}
            type={pending.type}
            incidentId={pending.incidentId}
            onCancel={() => setPending(null)}
            onDone={() => {
              setPending(null)
              reload()
              reloadAlerts()
            }}
          />
        </Modal>
      )}
    </>
  )
}
