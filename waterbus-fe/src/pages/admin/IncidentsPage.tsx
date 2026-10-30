import { useState } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  ActionButton,
  DataTable,
  Modal,
  Notice,
  PageHeader,
  StatusBadge,
} from '../../features/admin/components/AdminUi'
import { AlertCard, AlertsEmpty } from '../../features/admin/components/AlertParts'
import { pendingCountOf, sortAlerts } from '../../features/admin/alertUtils'
import type { WeatherAlert } from '../../features/admin/types'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  closeAlert,
  getAlertsWithTrips,
  getBoats,
  getOperationLogs,
  getOperationTrips,
  getRoutes,
} from '../../features/admin/services/adminService'
import type { AdminTrip, OperationType } from '../../features/admin/types'
import {
  DISPATCH_OPERATIONS,
  allowedOperationsOf,
  formatDateTime,
  isFinished,
  tripBoatCode,
  tripRouteName,
} from '../../features/admin/utils'
import { useFleetHub } from '../../features/admin/hooks/useFleetHub'
import { runWeatherCheck } from '../../features/admin/services/operationsService'
import type { WeatherCheckResult } from '../../features/admin/types'
import { useAuth } from '../../features/authentication/hook/authenticationHook'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import OperationForm, { OPERATION_LABEL } from '../../features/admin/components/OperationForm'

export default function IncidentsPage() {
  useDocumentTitle('Incidents')
  const { user } = useAuth()
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const logs = useFetch(getOperationLogs, [version])
  const [alertVersion, reloadAlerts] = useReload()
  const alerts = useFetch(getAlertsWithTrips, [alertVersion])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  const [pending, setPending] = useState<{ trip: AdminTrip; type: OperationType; incidentId?: string } | null>(null)
  const weatherCheck = useSubmit()
  const closing = useSubmit()
  const [checkResult, setCheckResult] = useState<WeatherCheckResult | null>(null)
  const [confirmClose, setConfirmClose] = useState<WeatherAlert | null>(null)
  // New alerts, emergencies and every status change (by any admin or a captain) show up without reloading.
  const refresh = () => {
    reload()
    reloadAlerts()
  }
  useFleetHub(
    { onAlert: reloadAlerts, onIncident: reloadAlerts, onTripStatus: refresh, onReconnected: refresh },
  )
  const state = combineFetches(trips, logs, alerts, routes, boats)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !logs.data || !alerts.data || !routes.data || !boats.data) return <PageLoader />

  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'
  const boatCode = (id: string) => boats.data?.find((b) => b.id === id)?.code ?? '—'
  const tripLabel = (id: string | null) => {
    const trip = trips.data?.find((x) => x.id === id)
    return trip ? `${tripBoatCode(trip, boatCode)} • ${formatDateTime(trip.departureTime)}` : '—'
  }
  const active = trips.data.filter((x) => !isFinished(x.status))
  const openAlerts = sortAlerts(alerts.data)
  const waitingTrips = openAlerts.reduce((sum, alert) => sum + pendingCountOf(alert), 0)

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
    { header: 'Route', render: (x) => tripRouteName(x, routeName) },
    { header: 'Boat', render: (x) => <span className="font-mono font-semibold">{tripBoatCode(x, boatCode)}</span> },
    {
      header: 'Status',
      render: (x) => (
        <span className="inline-flex flex-wrap items-center gap-2">
          <StatusBadge status={x.status} />
          {awaitingDecision.has(x.id) && (
            <span className="rounded-full bg-signal-amber/25 px-2 py-0.5 text-[11px] font-semibold text-deep-river">
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
          ...DISPATCH_OPERATIONS.filter((op) => allowedOperationsOf(x).includes(op)),
          ...(incidentId ? (['NoAction'] as OperationType[]) : []),
        ]
        return (
          <span className="inline-flex flex-wrap justify-end gap-1">
            {operations.map((op) => (
              <ActionButton
                key={op}
                tone={op === 'Cancel' || op === 'Terminate' ? 'danger' : 'secondary'}
                onClick={() =>
                  setPending({ trip: x, type: op, incidentId: ANSWERS_ALERT.includes(op) ? incidentId : undefined })
                }
              >
                {t(OPERATION_LABEL[op])}
              </ActionButton>
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
          <div className="mt-1 font-headline-md text-2xl font-bold text-deep-river">{openAlerts.length}</div>
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
            className={`mt-1 font-headline-md text-2xl font-bold ${
              waitingTrips > 0 ? 'text-coral-glow' : 'text-deep-river'
            }`}
          >
            {waitingTrips}
          </div>
        </div>
        <div className="flex flex-col justify-between gap-2 rounded-2xl border border-outline-variant/40 bg-white p-4">
          <ActionButton
            icon="cloud_sync"
            tone="secondary"
            disabled={weatherCheck.busy}
            onClick={async () => {
              setCheckResult(null)
              const result = await weatherCheck.run(runWeatherCheck)
              if (result) {
                setCheckResult(result)
                reloadAlerts()
              }
            }}
          >
            {weatherCheck.busy ? t('Working…') : t('Check the weather now')}
          </ActionButton>
          <span className="text-xs text-on-surface-variant">
            {checkResult
              ? t(
                  checkResult.incidentsResolved
                    ? 'Checked {routes} routes: {created} new, {updated} updated, {resolved} closed alerts.'
                    : 'Checked {routes} routes: {created} new, {updated} updated alerts.',
                  {
                    routes: checkResult.routesChecked,
                    created: checkResult.incidentsCreated,
                    updated: checkResult.incidentsUpdated,
                    resolved: checkResult.incidentsResolved ?? 0,
                  },
                )
              : t('The weather is also checked on a schedule.')}
          </span>
        </div>
      </div>
      {weatherCheck.error && (
        <div className="mb-3">
          <Notice tone="error">{weatherCheck.error}</Notice>
        </div>
      )}
      {closing.error && (
        <div className="mb-3">
          <Notice tone="error">{closing.error}</Notice>
        </div>
      )}

      <section className="mb-8 space-y-3" aria-label={t('Open alerts')}>
        {openAlerts.length === 0 ? (
          <AlertsEmpty />
        ) : (
          openAlerts.map((alert) => (
            <AlertCard
              key={alert.id}
              alert={alert}
              trips={trips.data ?? []}
              boatCode={boatCode}
              closing={closing.busy}
              onClose={setConfirmClose}
              onDecide={(trip, type, incidentId) => setPending({ trip, type, incidentId })}
            />
          ))
        )}
      </section>

      <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Trips in operation')}</h2>
      <DataTable columns={columns} rows={active} rowKey={(x) => x.id} empty="No trips are scheduled or running." />

      <h2 className="mb-2 mt-8 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Operation log')}</h2>
      <DataTable columns={logColumns} rows={logs.data} rowKey={(l) => l.id} empty="Nothing has been done yet." />

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
            <ActionButton tone="secondary" onClick={() => setConfirmClose(null)}>
              {t('Back')}
            </ActionButton>
            <ActionButton
              disabled={closing.busy}
              onClick={async () => {
                const alert = confirmClose
                await closing.run(() => closeAlert(alert.id))
                setConfirmClose(null)
                reloadAlerts()
              }}
            >
              {closing.busy ? t('Working…') : t('Close alert')}
            </ActionButton>
          </div>
        </Modal>
      )}

      {pending && (
        <Modal title={OPERATION_LABEL[pending.type]} onClose={() => setPending(null)}>
          <OperationForm
            trip={pending.trip}
            type={pending.type}
            by={user?.fullName ?? ''}
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
