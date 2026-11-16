/** Đơn vị gộp của báo cáo doanh thu (BE `RevenueGranularity`). */
export type RevenueGranularity = 'Day' | 'Month'

/** Một kỳ của báo cáo (BE `RevenuePointResponse`). `periodStart` là `YYYY-MM-DD` giờ Việt Nam, hoặc ngày 1 của tháng. */
export interface RevenuePoint {
  periodStart: string
  collected: number
  refunded: number
  net: number
}

/** Tổng cả khoảng thời gian (BE `RevenueTotalsResponse`). */
export interface RevenueTotals {
  collected: number
  refunded: number
  net: number
  paymentCount: number
  refundCount: number
}

/** Kết quả `GET /admin/reports/revenue` (BE `RevenueReportResponse`). */
export interface RevenueReport {
  from: string
  to: string
  granularity: RevenueGranularity
  totals: RevenueTotals
  points: RevenuePoint[]
}
