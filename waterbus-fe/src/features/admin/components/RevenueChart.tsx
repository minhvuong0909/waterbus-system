import { Bar, CartesianGrid, ComposedChart, Line, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { TooltipProps } from 'recharts'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import type { RevenueGranularity, RevenuePoint } from '../types'
import { formatAxisMoney, formatPeriod, formatReportDate } from '../utils/revenue'

// Các màu tiền tương ứng teal-flow, coral-glow và deep-river của ứng dụng.
const SERIES = [
  { key: 'collected', label: 'Collected', color: '#147A7E' },
  { key: 'refunded', label: 'Refunded', color: '#F08A6B' },
  { key: 'net', label: 'Net revenue', color: '#0D2538' },
] as const

function RevenueTooltip({ active, payload, granularity }: TooltipProps<number, string> & { granularity: RevenueGranularity }) {
  const point = payload?.[0]?.payload as RevenuePoint | undefined
  if (!active || !point) return null
  return <div className="min-w-[220px] rounded-xl border border-outline-variant/40 bg-white p-4 shadow-lg">
    <p className="mb-3 text-xs font-semibold text-on-surface-variant">{granularity === 'Month' ? formatPeriod(point.periodStart, granularity) : formatReportDate(point.periodStart)}</p>
    <dl className="space-y-2">{SERIES.map((series) => <div key={series.key} className="flex items-center justify-between gap-5 text-sm">
      <dt className="flex items-center gap-2 text-on-surface-variant"><span className="h-2 w-2 rounded-full" style={{ backgroundColor: series.color }} />{t(series.label)}</dt>
      <dd className="font-semibold tabular-nums text-deep-river">{formatVnd(point[series.key])}</dd>
    </div>)}</dl>
  </div>
}

/** Giữ đủ kỳ bằng 0, mốc gốc 0 và doanh thu thuần âm; không làm mượt để tránh bịa xu hướng. */
export default function RevenueChart({ points, granularity, summary }: {
  points: RevenuePoint[]
  granularity: RevenueGranularity
  summary: string
}) {
  return <>
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <span className="text-xs text-on-surface-variant">{t('Unit: VND')}</span>
      <ul className="flex flex-wrap gap-x-4 gap-y-2" aria-label={t('Chart legend')}>
        {SERIES.map((series) => <li key={series.key} className="flex items-center gap-2 text-xs text-on-surface-variant">
          {series.key === 'net' ? <span className="w-5 border-t-2 border-dashed border-deep-river" /> : <span className="h-2.5 w-2.5 rounded-sm" style={{ backgroundColor: series.color }} />}{t(series.label)}
        </li>)}
      </ul>
    </div>
    <div role="img" aria-label={summary} className="h-72 w-full min-w-0 sm:h-80">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={points} accessibilityLayer margin={{ top: 16, right: 12, bottom: 4, left: 0 }} barGap={3}>
          <CartesianGrid strokeDasharray="3 5" vertical={false} stroke="#e3e8eb" />
          <XAxis dataKey="periodStart" tickFormatter={(value: string) => formatPeriod(value, granularity)} tickLine={false}
            axisLine={false} minTickGap={48} tickMargin={14} padding={{ left: 24, right: 24 }} tick={{ fontSize: 11, fill: '#596573' }} />
          <YAxis tickFormatter={formatAxisMoney} tickLine={false} axisLine={false} width={54} tickMargin={8}
            tick={{ fontSize: 11, fill: '#596573' }} domain={[(min: number) => Math.min(0, min), (max: number) => max === 0 ? 1 : max * 1.12]} />
          <ReferenceLine y={0} stroke="#c3cbd2" />
          <Tooltip content={<RevenueTooltip granularity={granularity} />} cursor={{ fill: '#147A7E', fillOpacity: 0.04 }} />
          <Bar dataKey="collected" name="Collected" fill={SERIES[0].color} radius={[4, 4, 0, 0]} maxBarSize={24} isAnimationActive={false} />
          <Bar dataKey="refunded" name="Refunded" fill={SERIES[1].color} radius={[4, 4, 0, 0]} maxBarSize={24} isAnimationActive={false} />
          <Line dataKey="net" name="Net revenue" stroke={SERIES[2].color} strokeDasharray="5 4" strokeWidth={2} dot={points.length === 1 ? { r: 4, fill: SERIES[2].color } : false} activeDot={{ r: 5, stroke: '#fff', strokeWidth: 2 }} type="linear" isAnimationActive={false} />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  </>
}
