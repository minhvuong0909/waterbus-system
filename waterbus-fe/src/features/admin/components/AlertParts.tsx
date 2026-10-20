import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import { alertMetrics, alertNote, alertTone, decisionOperation, isCaptainReport, pendingCountOf, timeAgo } from '../alertUtils'
import type { AdminTrip, OperationType, WeatherAlert } from '../types'
import { allowedOperationsOf, formatDateTime, tripBoatCode, weatherAlertTitle } from '../utils'
import { StatusBadge } from './AdminUi'
import { OPERATION_LABEL } from './OperationForm'

/** Decisions that answer an alert for one trip (Resume and Close are routine trip operations). */
const ALERT_DECISIONS: OperationType[] = ['Suspend', 'Cancel', 'Terminate']

const formatKmh = (value: number) => t('{speed} km/h', { speed: value.toLocaleString('vi-VN') })

/** "Weather alert on route RT01", the captain's report, or the level and wind of a mock alert. */
function titleOf(alert: WeatherAlert): string {
  if (isCaptainReport(alert)) return t('Captain emergency report')
  return alert.routeCode ? t('Weather alert on route {route}', { route: alert.routeCode }) : weatherAlertTitle(alert)
}

function Metric({ icon, label, value }: { icon: string; label: string; value: string }) {
  return (
    <div className="flex items-center gap-2 rounded-lg bg-white/70 px-2.5 py-1.5">
      <Icon name={icon} className="text-[18px] text-teal-flow" />
      <div className="leading-tight">
        <div className="text-[11px] text-on-surface-variant">{label}</div>
        <div className="text-sm font-bold text-deep-river">{value}</div>
      </div>
    </div>
  )
}

/** Wind, gust and sky of a weather alert, as small tiles. Nothing when they are unknown. */
function AlertMetricsRow({ alert }: { alert: WeatherAlert }) {
  const { condition, windKmh, gustKmh } = alertMetrics(alert)
  if (condition === undefined && windKmh === undefined && gustKmh === undefined) return null
  return (
    <div className="mt-3 flex flex-wrap gap-2">
      {windKmh !== undefined && <Metric icon="air" label={t('Wind')} value={formatKmh(windKmh)} />}
      {gustKmh !== undefined && <Metric icon="storm" label={t('Gusts')} value={formatKmh(gustKmh)} />}
      {condition && <Metric icon="cloud" label={t('Sky')} value={condition} />}
    </div>
  )
}

/** "Decided 1 of 3 trips" with a bar. */
function DecisionProgress({ alert }: { alert: WeatherAlert }) {
  const total = alert.trips?.length ?? alert.affectedTripCount ?? 0
  if (total === 0) return null
  const decided = total - pendingCountOf(alert)
  return (
    <div className="mt-3">
      <div className="flex items-center justify-between text-xs">
        <span className="font-semibold text-deep-river">
          {t('Decided for {decided} of {total} trips', { decided, total })}
        </span>
        {decided < total && (
          <span className="font-semibold text-coral-glow">
            {t('{count} waiting', { count: total - decided })}
          </span>
        )}
      </div>
      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-white/80" aria-hidden="true">
        <div className="h-full rounded-full bg-teal-flow transition-all" style={{ width: `${(decided / total) * 100}%` }} />
      </div>
    </div>
  )
}

/**
 * One open alert: how serious it is, the measured weather, and every affected trip with the
 * admin's decision, or the buttons to make it. The alert never changes a trip by itself.
 */
export function AlertCard({
  alert,
  trips,
  boatCode,
  onDecide,
  onClose,
  closing,
}: {
  alert: WeatherAlert
  /** Trips in operation, to know what can still be done with each affected trip. */
  trips: AdminTrip[]
  boatCode: (boatId: string) => string
  onDecide: (trip: AdminTrip, type: OperationType, incidentId: string) => void
  onClose: (alert: WeatherAlert) => void
  closing: boolean
}) {
  const tone = alertTone(alert)
  const note = alertNote(t(alert.message))

  return (
    <article className={`rounded-2xl border border-l-4 border-outline-variant/40 bg-white p-4 shadow-sm ${tone.accent}`}>
      <div className="flex items-start gap-3">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${tone.badge}`}>
          <Icon name={tone.icon} className="text-[22px]" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${tone.badge}`}>
              {tone.label}
            </span>
            {alert.routeCode && (
              <span className="rounded-full bg-surface-container px-2 py-0.5 font-mono text-[11px] font-semibold text-deep-river">
                {alert.routeCode}
              </span>
            )}
            <span className="text-xs text-on-surface-variant" title={formatDateTime(alert.issuedAt)}>
              {timeAgo(alert.issuedAt)}
            </span>
          </div>
          <h3 className="mt-1 font-headline-sm text-base font-bold text-deep-river">{titleOf(alert)}</h3>
          <p className="text-xs text-on-surface-variant">
            {isCaptainReport(alert) ? t('Reported by the captain') : t('Raised by the weather check')}
          </p>
        </div>
        <button
          type="button"
          disabled={closing}
          onClick={() => onClose(alert)}
          className="inline-flex min-h-[36px] shrink-0 items-center gap-1 rounded-full border border-outline-variant px-3 text-xs font-semibold text-deep-river hover:bg-surface-container-low disabled:opacity-50"
        >
          <Icon name="close" className="text-[16px]" />
          {t('Close alert')}
        </button>
      </div>

      <AlertMetricsRow alert={alert} />
      {note && <p className="mt-3 text-sm text-on-surface-variant">{note}</p>}
      <DecisionProgress alert={alert} />

      {alert.trips && alert.trips.length > 0 && (
        <ul className="mt-3 divide-y divide-surface-container rounded-xl border border-surface-container">
          {alert.trips.map((link) => {
            const trip = trips.find((x) => x.id === link.tripId)
            const departure = link.departureTime ?? trip?.departureTime
            const status = link.tripStatus ?? trip?.status
            const decisions = trip
              ? [...ALERT_DECISIONS.filter((op) => allowedOperationsOf(trip).includes(op)), 'NoAction' as const]
              : []
            return (
              <li key={link.tripId} className="flex flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2.5">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2 text-sm font-semibold text-deep-river">
                    <Icon name="directions_boat" className="text-[18px] text-teal-flow" />
                    <span className="font-mono">{trip ? tripBoatCode(trip, boatCode) : '—'}</span>
                    <span>{departure ? formatDateTime(departure) : '—'}</span>
                    {status && <StatusBadge status={status} />}
                  </div>
                </div>
                {link.decision ? (
                  <span className="inline-flex items-center gap-1 rounded-full bg-teal-flow/15 px-2.5 py-1 text-xs font-semibold text-teal-flow">
                    <Icon name="check_circle" className="text-[16px]" />
                    {(() => {
                      const op = decisionOperation(link.decision)
                      return op ? t(OPERATION_LABEL[op]) : link.decision
                    })()}
                  </span>
                ) : decisions.length > 0 && trip ? (
                  <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('Decide for this trip')}>
                    {decisions.map((op) => (
                      <button
                        key={op}
                        type="button"
                        onClick={() => onDecide(trip, op, alert.id)}
                        className={`min-h-[36px] rounded-full px-3 text-xs font-semibold transition-colors ${
                          op === 'Cancel' || op === 'Terminate'
                            ? 'bg-coral-glow text-white hover:opacity-90'
                            : op === 'NoAction'
                              ? 'border border-outline-variant text-deep-river hover:bg-surface-container-low'
                              : 'bg-deep-river text-white hover:opacity-90'
                        }`}
                      >
                        {t(OPERATION_LABEL[op])}
                      </button>
                    ))}
                  </div>
                ) : (
                  <span className="rounded-full bg-signal-amber/25 px-2.5 py-1 text-xs font-semibold text-deep-river">
                    {t('Needs your decision')}
                  </span>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </article>
  )
}

/** Shown when no alert is open. */
export function AlertsEmpty() {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-dashed border-outline-variant bg-white p-4">
      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-flow/10 text-teal-flow">
        <Icon name="check_circle" className="text-[22px]" />
      </span>
      <div>
        <p className="font-semibold text-deep-river">{t('No weather warning right now.')}</p>
        <p className="text-sm text-on-surface-variant">
          {t('New alerts from the weather check or a captain appear here by themselves.')}
        </p>
      </div>
    </div>
  )
}

/** Compact strip for the live radar: how serious, where, and how many trips still need a decision. */
export function AlertBanner({ alert }: { alert: WeatherAlert }) {
  const tone = alertTone(alert)
  const { windKmh, gustKmh } = alertMetrics(alert)
  const pending = pendingCountOf(alert)
  return (
    <div
      role="alert"
      className={`flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-l-4 border-outline-variant/40 bg-white px-4 py-3 text-sm shadow-sm ${tone.accent}`}
    >
      <span className={`flex h-8 w-8 items-center justify-center rounded-lg ${tone.badge}`}>
        <Icon name={tone.icon} className="text-[18px]" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className={`rounded-full px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${tone.badge}`}>
            {tone.label}
          </span>
          <span className="font-semibold text-deep-river">{titleOf(alert)}</span>
        </div>
        <div className="mt-0.5 text-xs text-on-surface-variant">
          {[
            windKmh !== undefined ? `${t('Wind')} ${formatKmh(windKmh)}` : null,
            gustKmh !== undefined ? `${t('Gusts')} ${formatKmh(gustKmh)}` : null,
            timeAgo(alert.issuedAt),
          ]
            .filter(Boolean)
            .join(' · ')}
        </div>
      </div>
      {pending > 0 && (
        <span className="rounded-full bg-coral-glow/15 px-2.5 py-1 text-xs font-semibold text-coral-glow">
          {t('{count} trips need a decision', { count: pending })}
        </span>
      )}
      <Link
        to={ROUTES.adminIncidents}
        className="inline-flex min-h-[36px] items-center gap-1 rounded-full bg-deep-river px-3 text-xs font-semibold text-white hover:opacity-90"
      >
        {t('Review alert')}
        <Icon name="arrow_forward" className="text-[16px]" />
      </Link>
    </div>
  )
}
