import { t } from '../../../i18n'
import type { AdminTrip, OperationType, WeatherAlert } from '../../operations/types'
import { formatDateTime } from '../../../utils/time'
import { weatherAlertTitle } from '../../operations/utils'

export interface AlertMetrics {
  /** Sky condition as the weather service words it, e.g. "mưa nhẹ". */
  condition?: string
  windKmh?: number
  gustKmh?: number
}

// The weather guard writes "Cảnh báo thời tiết tuyến RT01: mưa nhẹ, gió 43,2 km/h, gió giật 68,4 km/h. …"
// (dấu phẩy thập phân kiểu Việt). Các con số được đọc lại để hiển thị; câu chữ vẫn là nguồn gốc.
const DESCRIPTION = /:\s*([^,.:]+),\s*gió\s+([\d.,]+)\s*km\/h(?:,\s*gió giật\s+([\d.,]+)\s*km\/h)?/i

const toNumber = (text: string | undefined) => {
  if (!text) return undefined
  const value = Number(text.replace(',', '.'))
  return Number.isFinite(value) ? value : undefined
}

/** Gió, gió giật và trời của một cảnh báo thời tiết, đọc từ câu mà Weather Guard viết. */
export function alertMetrics(alert: Pick<WeatherAlert, 'message'>): AlertMetrics {
  const match = alert.message.match(DESCRIPTION)
  if (match) return { condition: match[1].trim(), windKmh: toNumber(match[2]), gustKmh: toNumber(match[3]) }
  return {}
}

/** Câu mẫu mà Weather Guard thêm vào mọi cảnh báo; đã nói một lần ở đầu trang nên thẻ không lặp lại. */
export const isStandardNote = (note: string): boolean => /hệ thống không tự đổi trạng thái chuyến/i.test(note)

/** Phần sau các số đo trong thông điệp (lời khuyên cho Admin), hoặc cả thông điệp. */
export function alertNote(message: string): string {
  const match = message.match(DESCRIPTION)
  if (!match || match.index === undefined) return message
  return message
    .slice(match.index + match[0].length)
    .replace(/^\s*\.\s*/, '')
    .trim()
}

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

/** True khi báo cáo do người tạo (SOS của thuyền trưởng), không phải Weather Guard. */
export const isCaptainReport = (alert: Pick<WeatherAlert, 'kind'>): boolean =>
  Boolean(alert.kind && alert.kind !== 'Weather')

/** Số chuyến đã liên kết còn chờ Admin quyết định. */
export function pendingCountOf(alert: Pick<WeatherAlert, 'pendingDecisionCount' | 'trips'>): number {
  if (alert.trips) return alert.trips.filter((trip) => trip.decision === null).length
  return alert.pendingDecisionCount ?? 0
}

/** Nghiêm trọng nhất trước (mức 2 và báo cáo của thuyền trưởng), rồi mới nhất. */
export function sortAlerts(alerts: WeatherAlert[]): WeatherAlert[] {
  const weight = (alert: WeatherAlert) => (isCaptainReport(alert) || alert.level === 2 ? 1 : 0)
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
export function alertTone(alert: WeatherAlert): {
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
    ? { label: t('High risk'), icon: 'storm', accent: 'border-l-coral-glow', badge: 'bg-coral-glow/15 text-coral-glow' }
    : { label: t('Warning'), icon: 'air', accent: 'border-l-signal-amber', badge: 'bg-signal-amber/20 text-deep-river' }
}

/** "43,2 km/h". */
export const formatKmh = (value: number) => t('{speed} km/h', { speed: value.toLocaleString('vi-VN') })

/** "Cảnh báo thời tiết tuyến RT01", báo cáo của thuyền trưởng, hoặc mức cảnh báo. */
export function titleOf(alert: WeatherAlert): string {
  if (isCaptainReport(alert)) return t('Captain emergency report')
  return alert.routeCode ? t('Weather alert on route {route}', { route: alert.routeCode }) : weatherAlertTitle(alert)
}
