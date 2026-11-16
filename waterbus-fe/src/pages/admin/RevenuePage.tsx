import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, DataTable, ErrorState, Icon, PageHeader, PageLoader, Panel, SelectField, TextField } from '../../components/ui'
import type { Column } from '../../components/ui'
import RevenueChart from '../../features/admin/components/RevenueChart'
import RevenueSummaryCards from '../../features/admin/components/RevenueSummaryCards'
import { getRevenueReport } from '../../features/admin/services/revenueService'
import type { RevenueGranularity, RevenuePoint } from '../../features/admin/types'
import { formatPeriod, formatReportDate, REVENUE_RANGES, revenueCsv, revenueRange, revenueRangeError } from '../../features/admin/utils/revenue'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { saveBlob } from '../../utils/download'
import { formatVnd } from '../../utils/format'

/** Tiền theo ngày hoàn tất giao dịch, không phải ngày chạy tàu hay giá trị đơn hàng. */
export default function RevenuePage() {
  useDocumentTitle('Revenue')
  const [preset, setPreset] = useState('30')
  const [range, setRange] = useState(() => revenueRange(30, new Date()))
  const [draft, setDraft] = useState(range)
  const [rangeError, setRangeError] = useState<string | null>(null)
  const [granularity, setGranularity] = useState<RevenueGranularity>('Day')
  const [activeOnly, setActiveOnly] = useState(true)
  const report = useFetch(() => getRevenueReport(range.from, range.to, granularity), [range, granularity])
  const data = report.error ? undefined : report.data
  const chooseRange = (value: string) => {
    setPreset(value); setRangeError(null)
    if (value === 'custom') return
    const next = revenueRange(value === 'today' ? 1 : value === 'month' ? 1 : Number(value), new Date())
    if (value === 'month') next.from = `${next.to.slice(0, 7)}-01`
    setRange(next); setDraft(next)
  }
  const applyRange = (event: FormEvent) => {
    event.preventDefault()
    const error = revenueRangeError(draft.from, draft.to)
    setRangeError(error)
    if (!error) setRange({ ...draft })
  }
  const exportCsv = () => {
    if (!data || report.fetching) return
    const csv = revenueCsv(data, [t('Period'), `${t('Collected')} (VND)`, `${t('Refunded')} (VND)`, `${t('Net revenue')} (VND)`])
    saveBlob(new Blob([csv], { type: 'text/csv;charset=utf-8;' }), `revenue_${data.from}_${data.to}_${data.granularity.toLowerCase()}.csv`)
  }
  const activePoints = data?.points.filter((point) => point.collected !== 0 || point.refunded !== 0) ?? []
  const rows = [...(activeOnly ? activePoints : data?.points ?? [])].reverse()
  const columns: Column<RevenuePoint>[] = [
    { header: data?.granularity === 'Month' ? 'Month' : 'Date', render: (point) => <span className="font-semibold">{data?.granularity === 'Month' ? formatPeriod(point.periodStart, 'Month') : formatReportDate(point.periodStart)}</span> },
    { header: 'Collected', className: 'text-right', render: (point) => <span className="whitespace-nowrap tabular-nums">{formatVnd(point.collected)}</span> },
    { header: 'Refunded', className: 'text-right', render: (point) => <span className="whitespace-nowrap tabular-nums text-on-surface-variant">{formatVnd(point.refunded)}</span> },
    { header: 'Net revenue', className: 'text-right', render: (point) => <strong className={`whitespace-nowrap tabular-nums ${point.net < 0 ? 'text-coral-glow' : 'text-teal-flow'}`}>{formatVnd(point.net)}</strong> },
  ]
  const periodLabel = data ? `${formatReportDate(data.from)} — ${formatReportDate(data.to)}` : ''

  return <>
    <PageHeader title="Revenue" description="Track completed payments, refunds and net cash collected." actions={<>
      <Button variant="neutral" size="sm" icon="refresh" onClick={report.retry} disabled={report.fetching}>{t('Refresh')}</Button>
      <Button size="sm" icon="save" onClick={exportCsv} disabled={!data || report.fetching}>{t('Download CSV')}</Button>
    </>} />
    <section className="mb-5 rounded-2xl border border-outline-variant/30 bg-white p-4 shadow-card sm:p-5" aria-label={t('Report filters')}>
      <div className="grid items-end gap-4 sm:grid-cols-2 xl:grid-cols-[minmax(12rem,1fr)_minmax(12rem,1fr)_auto]">
        <SelectField label="Period" value={preset} onChange={(event) => chooseRange(event.target.value)}>
          <option value="today">{t('Today')}</option><option value="month">{t('This month')}</option>
          {REVENUE_RANGES.map((option) => <option key={option.value} value={option.value}>{t(option.label)}</option>)}
          <option value="custom">{t('Custom date range')}</option>
        </SelectField>
        <SelectField label="Group" value={granularity} onChange={(event) => setGranularity(event.target.value as RevenueGranularity)}>
          <option value="Day">{t('By day')}</option><option value="Month">{t('By month')}</option>
        </SelectField>
        <div className="flex min-h-[42px] items-center gap-2 text-xs text-on-surface-variant"><Icon name="schedule" className="text-lg text-teal-flow" />{t('Vietnam time · UTC+7')}</div>
      </div>
      {preset === 'custom' && <form onSubmit={applyRange} noValidate className="mt-4 border-t border-outline-variant/30 pt-4">
        <div className="grid items-end gap-3 sm:grid-cols-[1fr_1fr_auto]">
          <TextField label="From date" type="date" required value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} />
          <TextField label="To date" type="date" required value={draft.to} onChange={(event) => setDraft({ ...draft, to: event.target.value })} />
          <Button type="submit" size="sm">{t('Apply range')}</Button>
        </div>
        <p className={`mt-2 text-xs ${rangeError ? 'text-coral-glow' : 'text-on-surface-variant'}`} role={rangeError ? 'alert' : undefined}>{t(rangeError ?? 'Up to 366 days, including the start and end dates.')}</p>
      </form>}
    </section>
    {report.error ? <ErrorState onRetry={report.retry} /> : !data ? <PageLoader /> : <div className="space-y-5" aria-busy={report.fetching}>
      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-on-surface-variant">
        <p className="font-medium">{periodLabel}</p>
        <span role="status" className="inline-flex items-center gap-1.5 rounded-full border border-outline-variant/40 bg-white px-2.5 py-1"><span className={`h-1.5 w-1.5 rounded-full ${report.fetching ? 'bg-signal-amber' : 'bg-teal-flow'}`} />{t(report.fetching ? 'Updating report…' : 'Completed transactions only')}</span>
      </div>
      <div className={report.fetching ? 'pointer-events-none space-y-5 opacity-60' : 'space-y-5'}>
        <RevenueSummaryCards totals={data.totals} />
        <section className="overflow-hidden rounded-2xl border border-outline-variant/30 bg-white shadow-card">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-outline-variant/30 px-4 py-4 sm:px-6">
            <div><h2 className="font-display text-base font-bold text-deep-river">{t('Revenue over time')}</h2><p className="mt-1 text-xs text-on-surface-variant">{t('Cash collected and refunded in the selected period')}</p></div>
            <span className="rounded-full bg-teal-flow/10 px-3 py-1.5 text-xs font-semibold text-teal-flow">{t(data.granularity === 'Month' ? 'By month' : 'By day')}</span>
          </div>
          <div className="p-4 sm:p-6">
            {data.totals.paymentCount === 0 && data.totals.refundCount === 0 ? <div className="flex min-h-[260px] flex-col items-center justify-center text-center">
              <span className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-teal-flow/10 text-teal-flow"><Icon name="account_balance" className="text-2xl" /></span>
              <h3 className="text-sm font-semibold text-deep-river">{t('No completed transactions')}</h3><p className="mt-2 max-w-sm text-sm text-on-surface-variant">{t('Try another date range. Pending payments are not included in this report.')}</p>
            </div> : <RevenueChart points={data.points} granularity={data.granularity} summary={t('Collected {collected}, refunded {refunded}, net {net}.', { collected: formatVnd(data.totals.collected), refunded: formatVnd(data.totals.refunded), net: formatVnd(data.totals.net) })} />}
            <div className="mt-4 flex items-start gap-2 rounded-xl bg-mist px-3 py-2.5 text-xs leading-relaxed text-on-surface-variant"><Icon name="info" className="mt-0.5 shrink-0 text-base" /><p>{t('{active} of {total} periods have cash activity; all other periods are zero.', { active: activePoints.length, total: data.points.length })}{data.granularity === 'Month' && <> {t('Monthly figures include only dates inside the selected range.')}</>}</p></div>
          </div>
        </section>
        <div className="grid items-start gap-5 xl:grid-cols-[minmax(0,2fr)_minmax(16rem,1fr)]">
          <section className="min-w-0 overflow-hidden rounded-2xl border border-outline-variant/30 bg-white shadow-card">
            <div className="flex flex-wrap items-center justify-between gap-3 border-b border-outline-variant/30 px-4 py-4 sm:px-5">
              <div><h2 className="font-display text-base font-bold text-deep-river">{t('Revenue breakdown')}</h2><p className="mt-1 text-xs text-on-surface-variant">{t('Newest period first · Unit: VND')}</p></div>
              <label className="flex min-h-[44px] items-center gap-2 text-xs text-on-surface-variant"><input type="checkbox" checked={activeOnly} onChange={(event) => setActiveOnly(event.target.checked)} className="rounded border-outline-variant text-teal-flow focus:ring-teal-flow" />{t('Only periods with activity')}</label>
            </div>
            <div className="[&>div]:rounded-none [&>div]:border-0 [&>div]:shadow-none"><DataTable key={`${data.from}:${data.to}:${data.granularity}:${activeOnly}`} paginate columns={columns} rows={rows} rowKey={(point) => point.periodStart} empty="No cash activity in the selected period." /></div>
            <div className="flex flex-wrap justify-between gap-2 border-t border-outline-variant/30 bg-mist/40 px-4 py-3 text-xs text-on-surface-variant"><span>{t('{count} periods shown', { count: rows.length })}</span><span>{periodLabel}</span></div>
          </section>
          <Panel title="How this report is calculated">
            <div className="space-y-4 text-sm leading-relaxed text-on-surface-variant">
              <p><strong className="text-deep-river">{t('Recognition date')}</strong><br />{t('Recorded when a payment or refund completes, not on the sailing date.')}</p>
              <p><strong className="text-deep-river">{t('Net revenue')}</strong><br />{t('Completed payments minus completed refunds. It may be negative when refunds exceed receipts in this period.')}</p>
              <div className="border-t border-outline-variant/30 pt-4 text-xs">{t('Late payments waiting for reconciliation and legacy simulated refunds are not counted.')}</div>
            </div>
          </Panel>
        </div>
      </div>
    </div>}
  </>
}
