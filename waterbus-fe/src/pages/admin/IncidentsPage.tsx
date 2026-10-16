import { useState } from 'react'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import {
  ActionButton,
  DataTable,
  Modal,
  Notice,
  PageHeader,
  Panel,
  StatusBadge,
} from '../../features/admin/components/AdminUi'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  getBoats,
  getOperationLogs,
  getRoutes,
  getTrips,
  getWeatherAlerts,
  operateTrip,
} from '../../features/admin/services/adminService'
import type { AdminTrip, OperationType } from '../../features/admin/types'
import { DISPATCH_OPERATIONS, allowedOperations, formatDateTime, isFinished, weatherAlertTitle } from '../../features/admin/utils'
import { useFleetHub } from '../../features/admin/hooks/useFleetHub'
import { runWeatherCheck } from '../../features/admin/services/operationsService'
import type { WeatherCheckResult } from '../../features/admin/types'
import { USE_MOCK } from '../../constant/env'
import { useAuth } from '../../features/authentication/hook/authenticationHook'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'

const OPERATION_LABEL: Record<OperationType, string> = {
  Suspend: 'Suspend',
  Resume: 'Resume',
  Cancel: 'Cancel trip',
  Terminate: 'Terminate',
  Start: 'Start',
  Arrive: 'Arrive',
  SOS: 'SOS',
}

const OPERATION_HELP: Partial<Record<OperationType, string>> = {
  Suspend: 'The trip is put on hold (delayed) until you resume it.',
  Resume: 'The trip goes back to its schedule.',
  Cancel: 'The trip will not run. Passengers see it as cancelled.',
  Terminate: 'Ends a trip that is already sailing, for an emergency. It is recorded as cancelled.',
}

function OperationForm({
  trip,
  type,
  by,
  onCancel,
  onDone,
}: {
  trip: AdminTrip
  type: OperationType
  by: string
  onCancel: () => void
  onDone: () => void
}) {
  const [reason, setReason] = useState('')
  const [showError, setShowError] = useState(false)
  const { run, busy, error } = useSubmit()

  return (
    <form
      noValidate
      className="space-y-3"
      onSubmit={async (e) => {
        e.preventDefault()
        if (!reason.trim()) {
          setShowError(true)
          return
        }
        const done = await run(() => operateTrip(trip.id, type, reason.trim(), by))
        if (done) onDone()
      }}
    >
      <p className="text-sm text-on-surface-variant">
        {t('Trip of {time}.', { time: formatDateTime(trip.departureTime) })} {t(OPERATION_HELP[type] ?? '')}
      </p>
      <div>
        <label htmlFor="operation-reason" className="mb-1 block text-xs font-semibold text-on-surface-variant">
          {t('Reason')}
        </label>
        <textarea
          id="operation-reason"
          rows={3}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className="w-full rounded-lg border border-outline-variant px-3 py-2 text-sm text-deep-river focus:border-teal-flow focus:ring-teal-flow"
          aria-invalid={showError && !reason.trim()}
        />
        {showError && !reason.trim() && (
          <p role="alert" className="mt-1 text-[11px] text-coral-glow">
            {t('Give a reason, it is kept in the log.')}
          </p>
        )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Back')}
        </ActionButton>
        <ActionButton type="submit" tone={type === 'Cancel' || type === 'Terminate' ? 'danger' : 'primary'} disabled={busy}>
          {busy ? t('Working…') : t(OPERATION_LABEL[type])}
        </ActionButton>
      </div>
    </form>
  )
}

export default function IncidentsPage() {
  useDocumentTitle('Incidents')
  const { user } = useAuth()
  const [version, reload] = useReload()
  const trips = useFetch(getTrips, [version])
  const logs = useFetch(getOperationLogs, [version])
  const [alertVersion, reloadAlerts] = useReload()
  const alerts = useFetch(getWeatherAlerts, [alertVersion])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  const [pending, setPending] = useState<{ trip: AdminTrip; type: OperationType } | null>(null)
  const weatherCheck = useSubmit()
  const [checkResult, setCheckResult] = useState<WeatherCheckResult | null>(null)
  // New weather incidents appear as soon as the weather guard records them.
  useFleetHub({ onAlert: reloadAlerts, onReconnected: reloadAlerts }, !USE_MOCK)
  const state = combineFetches(trips, logs, alerts, routes, boats)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !logs.data || !alerts.data || !routes.data || !boats.data) return <PageLoader />

  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'
  const boatCode = (id: string) => boats.data?.find((b) => b.id === id)?.code ?? '—'
  const tripLabel = (id: string | null) => {
    const trip = trips.data?.find((x) => x.id === id)
    return trip ? `${boatCode(trip.boatId)} • ${formatDateTime(trip.departureTime)}` : '—'
  }
  const active = trips.data.filter((x) => !isFinished(x.status))

  const columns: Column<AdminTrip>[] = [
    { header: 'Departure', render: (x) => formatDateTime(x.departureTime) },
    { header: 'Route', render: (x) => routeName(x.routeId) },
    { header: 'Boat', render: (x) => <span className="font-mono font-semibold">{boatCode(x.boatId)}</span> },
    { header: 'Status', render: (x) => <StatusBadge status={x.status} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) => (
        <span className="inline-flex flex-wrap justify-end gap-1">
          {DISPATCH_OPERATIONS.filter((op) => allowedOperations(x.status).includes(op)).map((op) => (
            <ActionButton
              key={op}
              tone={op === 'Cancel' || op === 'Terminate' ? 'danger' : 'secondary'}
              onClick={() => setPending({ trip: x, type: op })}
            >
              {t(OPERATION_LABEL[op])}
            </ActionButton>
          ))}
        </span>
      ),
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

      {!USE_MOCK && (
        <div className="mb-4 flex flex-wrap items-center gap-3">
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
          {checkResult && (
            <span className="text-sm text-on-surface-variant">
              {t('Checked {routes} routes: {created} new, {updated} updated alerts.', {
                routes: checkResult.routesChecked,
                created: checkResult.incidentsCreated,
                updated: checkResult.incidentsUpdated,
              })}
            </span>
          )}
          {weatherCheck.error && <Notice tone="error">{weatherCheck.error}</Notice>}
        </div>
      )}

      {alerts.data.length === 0 && (
        <p className="mb-6 text-sm text-on-surface-variant">{t('No weather warning right now.')}</p>
      )}
      <div className="mb-6 grid gap-3 md:grid-cols-2">
        {alerts.data.map((alert) => (
          <Panel key={alert.id} className="border-signal-amber/50 bg-signal-amber/10">
            <div className="flex items-start gap-3">
              <Icon name="air" className="text-[24px] text-signal-amber" />
              <div className="text-sm">
                <div className="font-bold text-deep-river">{weatherAlertTitle(alert)}</div>
                <div className="text-on-surface-variant">{t(alert.message)}</div>
                <div className="mt-1 text-xs text-on-surface-variant">
                  {formatDateTime(alert.issuedAt)}
                  {alert.affectedTripCount != null &&
                    ` • ${t('{count} trips affected', { count: alert.affectedTripCount })}`}
                </div>
              </div>
            </div>
          </Panel>
        ))}
      </div>

      <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Trips in operation')}</h2>
      <DataTable columns={columns} rows={active} rowKey={(x) => x.id} empty="No trips are scheduled or running." />

      <h2 className="mb-2 mt-8 text-sm font-bold uppercase tracking-wide text-deep-river">{t('Operation log')}</h2>
      <DataTable columns={logColumns} rows={logs.data} rowKey={(l) => l.id} empty="Nothing has been done yet." />

      {pending && (
        <Modal title={OPERATION_LABEL[pending.type]} onClose={() => setPending(null)}>
          <OperationForm
            trip={pending.trip}
            type={pending.type}
            by={user?.fullName ?? ''}
            onCancel={() => setPending(null)}
            onDone={() => {
              setPending(null)
              reload()
            }}
          />
        </Modal>
      )}
    </>
  )
}
