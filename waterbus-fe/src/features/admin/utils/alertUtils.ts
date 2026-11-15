import { t } from '../../../i18n'
import type { AdminTrip, IncidentAlert, OperationType } from '../../operations/types'
import { formatDateTime } from '../../../utils/time'

/**
 * Thao tác ứng với một quyết định đã lưu. Backend giữ tên thao tác ("Suspend", "Cancel", "Terminate"), riêng "Không
 * cần xử lý" lưu là "None" (TripLifecycle.DecisionOf).
 */
export function decisionOperation(decision: string): OperationType | null {
  if (decision === 'None') return 'NoAction'
  const known: OperationType[] = ['Suspend', 'Cancel', 'Terminate', 'Resume', 'Complete', 'NoAction']
  return known.find((op) => op === decision) ?? null
}

/** Quyết định trả lời một sự cố cho chuyến (Resume và Close là thao tác thường ngày của chuyến, không nằm ở đây). */
const ALERT_DECISIONS: OperationType[] = ['Suspend', 'Cancel', 'Terminate']

/** Quyết định mọi chuyến đã chọn đều làm được, cộng "Không cần xử lý" (luôn hợp lệ với chuyến đang chờ). */
export function commonDecisions(selected: Pick<AdminTrip, 'allowedOperations'>[]): OperationType[] {
  if (selected.length === 0) return []
  return [...ALERT_DECISIONS.filter((op) => selected.every((trip) => trip.allowedOperations.includes(op))), 'NoAction']
}

/** True khi sự cố là báo khẩn cấp của thuyền trưởng: dựa vào nguồn tạo (`Source`), không đoán qua loại sự cố. */
export const isCaptainReport = (alert: Pick<IncidentAlert, 'source'>): boolean => alert.source === 'Captain'

/** Số chuyến đã liên kết còn chờ Admin quyết định. */
export function pendingCountOf(alert: Pick<IncidentAlert, 'pendingDecisionCount' | 'trips'>): number {
  if (alert.trips) return alert.trips.filter((trip) => trip.decision === null).length
  return alert.pendingDecisionCount ?? 0
}

/** Nghiêm trọng nhất trước (mức 2 và báo cáo của thuyền trưởng), rồi mới nhất. */
export function sortAlerts(alerts: IncidentAlert[]): IncidentAlert[] {
  const weight = (alert: IncidentAlert) => (isCaptainReport(alert) || alert.level === 2 ? 1 : 0)
  return [...alerts].sort(
    (a, b) => weight(b) - weight(a) || Date.parse(b.issuedAt) - Date.parse(a.issuedAt),
  )
}

/** "vừa xong", "12 phút trước", "3 giờ trước", hoặc ngày giờ khi đã quá một ngày. */
export function timeAgo(iso: string, now = Date.now()): string {
  const minutes = Math.floor((now - Date.parse(iso)) / 60_000)
  if (minutes < 1) return t('just now')
  if (minutes < 60) return t('{count} min ago', { count: minutes })
  if (minutes < 24 * 60) return t('{count} h ago', { count: Math.floor(minutes / 60) })
  return formatDateTime(iso)
}

/** Nhãn và màu của cảnh báo theo mức độ nghiêm trọng. */
export function alertTone(alert: IncidentAlert): {
  label: string
  icon: string
  /** Viền trái và ô icon. */
  accent: string
  badge: string
} {
  if (isCaptainReport(alert)) {
    return { label: t('Captain report'), icon: 'sos', accent: 'border-l-coral-glow', badge: 'bg-coral-glow/15 text-coral-glow' }
  }
  return alert.level === 2
    ? { label: t('High risk'), icon: 'warning', accent: 'border-l-coral-glow', badge: 'bg-coral-glow/15 text-coral-glow' }
    : { label: t('Warning'), icon: 'info', accent: 'border-l-signal-amber', badge: 'bg-signal-amber/20 text-deep-river' }
}

/** Báo cáo khẩn cấp của thuyền trưởng hoặc tên chung của sự cố. */
export function titleOf(alert: Pick<IncidentAlert, 'source'>): string {
  return isCaptainReport(alert) ? t('Captain emergency report') : t('Operational incident')
}
