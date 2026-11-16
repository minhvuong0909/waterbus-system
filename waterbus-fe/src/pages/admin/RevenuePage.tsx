import { useMemo, useState } from 'react'
import { ErrorState, FilterChips, PageHeader, PageLoader, Panel, StatCard } from '../../components/ui'
import RevenueChart from '../../features/admin/components/RevenueChart'
import { getRevenueReport } from '../../features/admin/services/revenueService'
import type { RevenueGranularity } from '../../features/admin/types'
import { REVENUE_GRANULARITIES, REVENUE_RANGES, revenueRange } from '../../features/admin/utils/revenue'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { formatVnd } from '../../utils/format'

/**
 * Doanh thu cho Admin (BR-CFG-02): tiền thực thu, đã hoàn và thuần theo ngày hoàn tất giao dịch (giờ Việt Nam).
 * Số liệu do backend tính; trang chỉ chọn khoảng thời gian và vẽ.
 */
export default function RevenuePage() {
  useDocumentTitle('Revenue')
  const [rangeDays, setRangeDays] = useState('30')
  const [granularity, setGranularity] = useState<RevenueGranularity>('Day')
  // Mốc "hôm nay" chốt khi mở trang hoặc đổi khoảng, để mỗi lần đổi bộ lọc là một truy vấn mới.
  const range = useMemo(() => revenueRange(Number(rangeDays), new Date()), [rangeDays])
  const report = useFetch(() => getRevenueReport(range.from, range.to, granularity), [range, granularity])

  if (report.error) return <ErrorState onRetry={report.retry} />
  if (report.loading || !report.data) return <PageLoader />

  const { totals, points } = report.data
  const isEmpty = totals.paymentCount === 0 && totals.refundCount === 0
  const summary = t('Collected {collected}, refunded {refunded}, net {net}.', {
    collected: formatVnd(totals.collected),
    refunded: formatVnd(totals.refunded),
    net: formatVnd(totals.net),
  })

  return (
    <>
      <PageHeader title="Revenue" description="Money actually collected and refunded, by the day each payment completed." />
      <div className="mb-4 flex flex-wrap gap-4">
        <FilterChips label="Period" value={rangeDays} onChange={setRangeDays} options={REVENUE_RANGES} />
        <FilterChips label="Group" value={granularity} onChange={(value) => setGranularity(value as RevenueGranularity)} options={REVENUE_GRANULARITIES} />
      </div>
      <div className="grid grid-cols-1 gap-3 xl:grid-cols-3">
        <StatCard label="Collected" value={formatVnd(totals.collected)} hint="{count} payments" hintCount={totals.paymentCount} />
        <StatCard label="Refunded" value={formatVnd(totals.refunded)} hint="{count} refunds" hintCount={totals.refundCount} />
        <StatCard label="Net revenue" value={formatVnd(totals.net)} hint="Collected minus refunded" />
      </div>
      <Panel
        className="mt-4"
        title="Revenue over time"
        description="Late payments waiting for reconciliation and legacy simulated refunds are not counted."
      >
        <div aria-busy={report.fetching} className={report.fetching ? 'opacity-60 transition-opacity' : 'transition-opacity'}>
          {isEmpty ? (
            <p className="py-10 text-center text-sm text-on-surface-variant">{t('No payments or refunds in this period.')}</p>
          ) : (
            <RevenueChart points={points} granularity={granularity} summary={summary} />
          )}
        </div>
      </Panel>
    </>
  )
}
