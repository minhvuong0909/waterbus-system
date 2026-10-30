import { t } from '../../i18n'
import type { OperationType, WeatherAlert } from './types'
import { formatDateTime } from '../../utils/time'

export interface AlertMetrics {
  /** Sky condition as the weather service words it, e.g. "mưa nhẹ". */
  condition?: string
  windKmh?: number
  gustKmh?: number
}

// The weather guard writes "Cảnh báo thời tiết tuyến RT01: mưa nhẹ, gió 43,2 km/h, gió giật 68,4 km/h. …"
// (Vietnamese decimal comma). The numbers are read back for display; the text stays the source.
const DESCRIPTION = /:\s*([^,.:]+),\s*gió\s+([\d.,]+)\s*km\/h(?:,\s*gió giật\s+([\d.,]+)\s*km\/h)?/i

const toNumber = (text: string | undefined) => {
  if (!text) return undefined
  const value = Number(text.replace(',', '.'))
  return Number.isFinite(value) ? value : undefined
}

/** Wind, gust and sky of a weather alert, read from the text the weather guard writes. */
export function alertMetrics(alert: Pick<WeatherAlert, 'message'>): AlertMetrics {
  const match = alert.message.match(DESCRIPTION)
  if (match) return { condition: match[1].trim(), windKmh: toNumber(match[2]), gustKmh: toNumber(match[3]) }
  return {}
}

/** The part of the message after the measurements (the advice to the admin), or the whole message. */
export function alertNote(message: string): string {
  const match = message.match(DESCRIPTION)
  if (!match || match.index === undefined) return message
  return message
    .slice(match.index + match[0].length)
    .replace(/^\s*\.\s*/, '')
    .trim()
}

/**
 * The operation behind a stored decision. The backend keeps the operation name ("Suspend",
 * "Cancel", "Terminate"), except "No action needed", which it stores as "None" (TripLifecycle.DecisionOf).
 */
export function decisionOperation(decision: string): OperationType | null {
  if (decision === 'None') return 'NoAction'
  const known: OperationType[] = ['Suspend', 'Cancel', 'Terminate', 'Resume', 'Complete', 'NoAction']
  return known.find((op) => op === decision) ?? null
}

/** True for a report raised by a person (captain SOS), not by the weather guard. */
export const isCaptainReport = (alert: Pick<WeatherAlert, 'kind'>): boolean =>
  Boolean(alert.kind && alert.kind !== 'Weather')

/** Linked trips still waiting for the admin's decision. */
export function pendingCountOf(alert: Pick<WeatherAlert, 'pendingDecisionCount' | 'trips'>): number {
  if (alert.trips) return alert.trips.filter((trip) => trip.decision === null).length
  return alert.pendingDecisionCount ?? 0
}

/** Most serious first (level 2 and captain reports), then the newest. */
export function sortAlerts(alerts: WeatherAlert[]): WeatherAlert[] {
  const weight = (alert: WeatherAlert) => (isCaptainReport(alert) || alert.level === 2 ? 1 : 0)
  return [...alerts].sort(
    (a, b) => weight(b) - weight(a) || Date.parse(b.issuedAt) - Date.parse(a.issuedAt),
  )
}

/** "just now", "12 min ago", "3 h ago", or the date and time once it is a day old. */
export function timeAgo(iso: string, now = Date.now()): string {
  const minutes = Math.floor((now - Date.parse(iso)) / 60_000)
  if (minutes < 1) return t('just now')
  if (minutes < 60) return t('{count} min ago', { count: minutes })
  if (minutes < 24 * 60) return t('{count} h ago', { count: Math.floor(minutes / 60) })
  return formatDateTime(iso)
}

/** Label and colours of an alert by how serious it is. */
export function alertTone(alert: WeatherAlert): {
  label: string
  icon: string
  /** Left edge and icon tile. */
  accent: string
  badge: string
} {
  if (isCaptainReport(alert)) {
    return { label: t('Captain report'), icon: 'sos', accent: 'border-l-coral-glow', badge: 'bg-coral-glow/15 text-coral-glow' }
  }
  return alert.level === 2
    ? { label: t('High risk'), icon: 'storm', accent: 'border-l-coral-glow', badge: 'bg-coral-glow/15 text-coral-glow' }
    : { label: t('Warning'), icon: 'air', accent: 'border-l-signal-amber', badge: 'bg-signal-amber/20 text-deep-river' }
}
