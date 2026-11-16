import { formatNumber } from '../../../utils/format'
import { toVietnamDate } from '../../../utils/time'
import type { RevenueGranularity, RevenueReport } from '../types'

const DAY_MS = 24 * 60 * 60 * 1000

/** Khoảng xem nhanh: số ngày gần nhất tính cả hôm nay. */
export const REVENUE_RANGES = [
  { value: '7', label: '7 days' },
  { value: '30', label: '30 days' },
  { value: '90', label: '90 days' },
  { value: '365', label: '1 year' },
]

export const REVENUE_GRANULARITIES: { value: RevenueGranularity; label: string }[] = [
  { value: 'Day', label: 'By day' },
  { value: 'Month', label: 'By month' },
]

/** `days` ngày gần nhất tới hôm nay, theo ngày Việt Nam (không theo múi giờ máy). */
export function revenueRange(days: number, now: Date): { from: string; to: string } {
  return { from: toVietnamDate(new Date(now.getTime() - (days - 1) * DAY_MS)), to: toVietnamDate(now) }
}

/** `2026-11-05` → `05/11` (theo ngày) hoặc `11/2026` (theo tháng). Cắt chuỗi, không qua `Date` nên không lệch múi giờ. */
export function formatPeriod(periodStart: string, granularity: RevenueGranularity): string {
  const [year, month, day] = periodStart.split('-')
  return granularity === 'Month' ? `${month}/${year}` : `${day}/${month}`
}

/** Nhãn trục tiền gọn: 1500000 → `1,5M`, 25000 → `25K`. */
export function formatAxisMoney(amount: number): string {
  const compact = (value: number) => value.toLocaleString('vi-VN', { maximumFractionDigits: 1 })
  if (Math.abs(amount) >= 1_000_000) return `${compact(amount / 1_000_000)}M`
  if (Math.abs(amount) >= 1_000) return `${compact(amount / 1_000)}K`
  return formatNumber(amount)
}

/** Ngày lịch Việt Nam từ API, không chuyển qua múi giờ trình duyệt. */
export function formatReportDate(value: string): string {
  const [year, month, day] = value.split('-')
  return `${day}/${month}/${year}`
}

/** Khớp giới hạn 366 ngày, gồm cả hai đầu, của API báo cáo. */
export function revenueRangeError(from: string, to: string): string | null {
  const valid = (value: string) => /^\d{4}-\d{2}-\d{2}$/.test(value) && Number(value.slice(0, 4)) >= 1 &&
    Number.isFinite(Date.parse(`${value}T00:00:00Z`)) && new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value
  if (!valid(from) || !valid(to)) return 'Choose valid start and end dates.'
  if (to < from) return 'The end date must be on or after the start date.'
  if ((Date.parse(to) - Date.parse(from)) / DAY_MS >= 366) return 'Choose a range of up to 366 days.'
  return null
}

/** Xuất đầy đủ kỳ đã trả về (kể cả kỳ bằng 0), số tiền dạng số để có thể đối chiếu. */
export function revenueCsv(report: RevenueReport, headers: string[]): string {
  const quote = (value: string) => `"${value.replace(/"/g, '""')}"`
  // BOM gi\u1EEF ti\u1EBFng Vi\u1EC7t \u0111\u00FAng trong Excel; d\u00F2ng `sep=,` \u00E9p Excel t\u00E1ch c\u1ED9t b\u1EB1ng d\u1EA5u ph\u1EA9y
  // k\u1EC3 c\u1EA3 m\u00E1y \u0111\u1EB7t d\u1EA5u ph\u00E2n c\u00E1ch danh s\u00E1ch l\u00E0 `;` (Windows ti\u1EBFng Vi\u1EC7t/ch\u00E2u \u00C2u).
  return '\uFEFFsep=,\r\n' + [headers.map(quote).join(','), ...report.points.map((point) =>
    [quote(point.periodStart), point.collected, point.refunded, point.net].join(','))].join('\r\n')
}
