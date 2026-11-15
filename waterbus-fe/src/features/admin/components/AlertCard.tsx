import { useState } from 'react'
import { Button, ErrorState, Icon } from '../../../components/ui'
import { CHECKBOX_CLASS, INSET_CLASS } from '../../../components/ui/styles'
import { t } from '../../../i18n'
import { useFetch } from '../../../hooks'
import { alertTone, commonDecisions, decisionOperation, isCaptainReport, timeAgo, titleOf } from '../utils/alertUtils'
import { getIncidentTrips } from '../../operations/services/operationsService'
import type { AdminTrip, AlertTrip, IncidentAlert, OperationType } from '../../operations/types'
import { formatDateTime } from '../../../utils/time'
import { OPERATION_LABEL } from '../../operations/utils'
import TripStatusBadge from '../../trips/components/TripStatusBadge'

/** "Đã quyết định 1/20 chuyến" kèm thanh tiến độ, theo số đếm của danh sách sự cố. */
function DecisionProgress({ total, pending }: { total: number; pending: number }) {
  if (total === 0) return null
  const decided = total - pending
  return (
    <div className="mt-3">
      <div className="flex items-center justify-between text-xs">
        <span className="font-semibold text-deep-river">
          {t('Decided for {decided} of {total} trips', { decided, total })}
        </span>
        {pending > 0 && (
          <span className="font-semibold text-coral-glow">{t('{count} waiting', { count: pending })}</span>
        )}
      </div>
      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-surface-container" aria-hidden="true">
        <div className="h-full rounded-full bg-teal-flow transition-all" style={{ width: `${(decided / total) * 100}%` }} />
      </div>
    </div>
  )
}

function DecisionChip({ decision }: { decision: string }) {
  const op = decisionOperation(decision)
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-teal-flow/15 px-2.5 py-1 text-xs font-semibold text-teal-flow">
      <Icon name="check_circle" className="text-base" />
      {op ? t(OPERATION_LABEL[op]) : decision}
    </span>
  )
}

function TripLine({ link, trip }: { link: AlertTrip; trip?: AdminTrip }) {
  const departure = link.departureTime ?? trip?.departureTime
  const status = link.tripStatus ?? trip?.status
  return (
    <span className="flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 text-sm text-deep-river">
      <span className="font-mono font-semibold">{trip?.boatCode ?? '—'}</span>
      <span>{departure ? formatDateTime(departure) : '—'}</span>
      {status && <TripStatusBadge status={status} />}
    </span>
  )
}

/**
 * Các chuyến của một sự cố, chỉ tải khi Admin mở sự cố. Chuyến đang chờ có ô chọn; Admin chọn một hoặc nhiều chuyến
 * rồi đưa ra một quyết định chung. Chuyến đã quyết định gom vào phần thu gọn bên dưới.
 */
function AlertTrips({
  alert,
  trips,
  version,
  onDecide,
}: {
  alert: IncidentAlert
  trips: AdminTrip[]
  version: number
  onDecide: (trips: AdminTrip[], type: OperationType, incidentId: string) => void
}) {
  const links = useFetch(() => getIncidentTrips(alert.id), [alert.id, version])
  const [selectedIds, setSelectedIds] = useState<string[]>([])

  if (links.error) return <ErrorState compact onRetry={links.retry} />
  if (!links.data) {
    return (
      <div role="status" aria-label={t('Loading')} className="mt-4 flex justify-center py-4">
        <span className="h-6 w-6 animate-spin rounded-full border-2 border-outline-variant border-t-teal-flow" />
      </div>
    )
  }

  const byId = new Map(trips.map((trip) => [trip.id, trip]))
  const pending = links.data.filter((link) => link.decision === null)
  const decided = links.data.filter((link) => link.decision !== null)
  // Chỉ chọn được chuyến còn trong danh sách vận hành (biết các thao tác được phép của nó).
  const selectable = pending.filter((link) => byId.has(link.tripId))
  // Sau khi tải lại, chuyến vừa được quyết định (ở đây hoặc bởi Admin khác) tự rời khỏi lựa chọn.
  const selected = selectable
    .filter((link) => selectedIds.includes(link.tripId))
    .map((link) => byId.get(link.tripId) as AdminTrip)
  const allSelected = selectable.length > 0 && selected.length === selectable.length
  const toggle = (id: string) =>
    setSelectedIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]))

  return (
    <div className="mt-4 space-y-3">
      {pending.length > 0 && (
        <div className={INSET_CLASS}>
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-outline-variant/30 bg-surface-container-low px-3 py-2">
            <label className="flex min-h-[36px] items-center gap-2 text-sm font-semibold text-deep-river">
              <input
                type="checkbox"
                checked={allSelected}
                disabled={selectable.length === 0}
                onChange={() => setSelectedIds(allSelected ? [] : selectable.map((link) => link.tripId))}
                className={CHECKBOX_CLASS}
              />
              {selected.length > 0
                ? t('{count} selected', { count: selected.length })
                : t('Waiting for a decision ({count})', { count: pending.length })}
            </label>
            {selected.length > 0 && (
              <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('Decide for the selected trips')}>
                {commonDecisions(selected).map((op) => (
                  <Button
                    key={op}
                    size="sm"
                    variant={op === 'Cancel' || op === 'Terminate' ? 'danger' : 'neutral'}
                    onClick={() => onDecide(selected, op, alert.id)}
                  >
                    {t(OPERATION_LABEL[op])}
                  </Button>
                ))}
              </div>
            )}
          </div>
          <ul className="max-h-80 divide-y divide-outline-variant/30 overflow-y-auto">
            {pending.map((link) => {
              const trip = byId.get(link.tripId)
              return (
                <li key={link.tripId}>
                  <label className={`flex min-h-[44px] items-center gap-3 px-3 py-2 ${trip ? 'cursor-pointer hover:bg-surface-container-low' : ''}`}>
                    <input
                      type="checkbox"
                      checked={selectedIds.includes(link.tripId)}
                      disabled={!trip}
                      onChange={() => toggle(link.tripId)}
                      className={CHECKBOX_CLASS}
                    />
                    <TripLine link={link} trip={trip} />
                  </label>
                </li>
              )
            })}
          </ul>
        </div>
      )}
      {decided.length > 0 && (
        <details className={INSET_CLASS}>
          <summary className="cursor-pointer px-3 py-2 text-sm font-semibold text-deep-river">
            {t('Already decided ({count})', { count: decided.length })}
          </summary>
          <ul className="divide-y divide-outline-variant/30 border-t border-outline-variant/30">
            {decided.map((link) => (
              <li key={link.tripId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                <TripLine link={link} trip={byId.get(link.tripId)} />
                <DecisionChip decision={link.decision ?? ''} />
              </li>
            ))}
          </ul>
        </details>
      )}
      {links.data.length === 0 && <p className="text-sm text-on-surface-variant">{t('No trip is linked to this alert.')}</p>}
    </div>
  )
}

/**
 * Một sự cố đang mở: mức độ, số đo thời tiết và tiến độ quyết định. Danh sách chuyến thu gọn, chỉ mở khi Admin cần
 * (SOS của thuyền trưởng mở sẵn). Sự cố không bao giờ tự đổi trạng thái chuyến.
 */
export default function AlertCard({
  alert,
  trips,
  version,
  onDecide,
  onClose,
}: {
  alert: IncidentAlert
  /** Chuyến trong danh sách vận hành, để biết còn làm được gì với từng chuyến. */
  trips: AdminTrip[]
  /** Đổi khi cần tải lại danh sách chuyến của sự cố (sau một quyết định, hoặc khi hub báo thay đổi). */
  version: number
  onDecide: (trips: AdminTrip[], type: OperationType, incidentId: string) => void
  onClose: (alert: IncidentAlert) => void
}) {
  const [open, setOpen] = useState(isCaptainReport(alert))
  const tone = alertTone(alert)
  const total = alert.affectedTripCount ?? 0
  const pending = alert.pendingDecisionCount ?? 0
  const listId = `alert-trips-${alert.id}`

  return (
    <article className={`rounded-2xl border border-l-4 border-outline-variant/30 bg-white p-4 shadow-sm ${tone.accent}`}>
      <div className="flex items-start gap-3">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${tone.badge}`}>
          <Icon name={tone.icon} className="text-2xl" />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className={`rounded-full px-2 py-0.5 text-2xs font-bold ${tone.badge}`}>
              {tone.label}
            </span>
            {alert.routeCode && (
              <span className="rounded-full bg-surface-container px-2 py-0.5 font-mono text-2xs font-semibold text-deep-river">
                {alert.routeCode}
              </span>
            )}
            <span className="text-xs text-on-surface-variant" title={formatDateTime(alert.issuedAt)}>
              {timeAgo(alert.issuedAt)}
            </span>
          </div>
          <h3 className="mt-1 font-display text-base font-bold text-deep-river">{titleOf(alert)}</h3>
          <p className="text-xs text-on-surface-variant">
            {isCaptainReport(alert) ? t('Reported by the captain') : t('Reported by operations')}
          </p>
        </div>
        <Button size="sm" variant="neutral" icon="close" onClick={() => onClose(alert)}>
          {t('Close alert')}
        </Button>
      </div>

      {alert.message && <p className="mt-3 text-sm text-on-surface-variant">{alert.message}</p>}
      <DecisionProgress total={total} pending={pending} />

      {total > 0 && (
        <button
          type="button"
          aria-expanded={open}
          aria-controls={listId}
          onClick={() => setOpen((value) => !value)}
          className="mt-3 inline-flex min-h-[40px] items-center gap-1 rounded-xl px-2 text-sm font-semibold text-teal-flow hover:bg-teal-flow/5"
        >
          <Icon name="expand_more" className={`text-lg transition-transform ${open ? 'rotate-180' : ''}`} />
          {open ? t('Hide trips') : t('Show {count} trips', { count: total })}
        </button>
      )}
      {open && (
        <div id={listId}>
          <AlertTrips alert={alert} trips={trips} version={version} onDecide={onDecide} />
        </div>
      )}
    </article>
  )
}
