import { formatNumber } from '../../../utils/format'
import { toVietnamDate } from '../../../utils/time'
import type { RevenueGranularity } from '../types'

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
