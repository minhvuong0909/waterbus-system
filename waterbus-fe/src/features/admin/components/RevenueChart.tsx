import { Bar, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import type { RevenueGranularity, RevenuePoint } from '../types'
import { formatAxisMoney, formatPeriod } from '../utils/revenue'

// Màu theo bảng màu của ứng dụng (tailwind.config.ts): teal-flow, coral-glow, deep-river.
const COLLECTED_COLOR = '#147A7E'
const REFUNDED_COLOR = '#F08A6B'
const NET_COLOR = '#0D2538'

/** Cột thực thu và đã hoàn, đường doanh thu thuần của từng kỳ. Số liệu đã được BE điền đủ các kỳ không có giao dịch. */
export default function RevenueChart({ points, granularity, summary }: {
  points: RevenuePoint[]
  granularity: RevenueGranularity
  /** Câu tóm tắt cho trình đọc màn hình. */
  summary: string
}) {
  return (
    <div role="img" aria-label={summary} className="h-72 w-full sm:h-80">
      <ResponsiveContainer width="100%" height="100%">
        <ComposedChart data={points} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#c3c7cd" />
          <XAxis
            dataKey="periodStart"
            tickFormatter={(value: string) => formatPeriod(value, granularity)}
            tickLine={false}
            minTickGap={16}
            tick={{ fontSize: 12 }}
          />
          <YAxis tickFormatter={formatAxisMoney} tickLine={false} axisLine={false} width={56} tick={{ fontSize: 12 }} />
          <Tooltip
            labelFormatter={(value: string) => formatPeriod(value, granularity)}
            formatter={(value: number, name: string) => [formatVnd(value), t(name)]}
          />
          <Legend formatter={(name: string) => t(name)} />
          <Bar dataKey="collected" name="Collected" fill={COLLECTED_COLOR} radius={[4, 4, 0, 0]} maxBarSize={32} />
          <Bar dataKey="refunded" name="Refunded" fill={REFUNDED_COLOR} radius={[4, 4, 0, 0]} maxBarSize={32} />
          <Line dataKey="net" name="Net revenue" stroke={NET_COLOR} strokeWidth={2} dot={false} type="linear" />
        </ComposedChart>
      </ResponsiveContainer>
    </div>
  )
}
