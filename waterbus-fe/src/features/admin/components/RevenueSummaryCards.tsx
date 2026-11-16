import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatNumber } from '../../../utils/format'
import type { RevenueTotals } from '../types'

export default function RevenueSummaryCards({ totals }: { totals: RevenueTotals }) {
  const cards = [
    { key: 'net', label: 'Net revenue', amount: totals.net, icon: 'account_balance', hint: 'Collected minus refunded', primary: true },
    { key: 'collected', label: 'Collected', amount: totals.collected, icon: 'payments', hint: '{count} completed payments', count: totals.paymentCount },
    { key: 'refunded', label: 'Refunded', amount: totals.refunded, icon: 'receipt_long', hint: '{count} completed refunds', count: totals.refundCount },
  ]
  return <div className="grid gap-3 md:grid-cols-3">
    {cards.map((card) => <section key={card.key} className={`relative overflow-hidden rounded-2xl border p-5 shadow-card sm:p-6 ${card.primary ? 'border-deep-river bg-deep-river text-white' : 'border-outline-variant/30 bg-white text-deep-river'}`}>
      <div className="flex items-center justify-between gap-2">
        <h2 className={`text-sm font-medium ${card.primary ? 'text-white/80' : 'text-on-surface-variant'}`}>{t(card.label)}</h2>
        <span className={`flex h-10 w-10 items-center justify-center rounded-xl ${card.primary ? 'bg-white/10 text-white' : card.key === 'refunded' ? 'bg-coral-glow/15 text-deep-river' : 'bg-teal-flow/10 text-teal-flow'}`}><Icon name={card.icon} className="text-xl" /></span>
      </div>
      <p className="mt-4 flex flex-wrap items-baseline gap-x-2 gap-y-1"><strong className="font-display text-3xl font-bold leading-tight tracking-tight tabular-nums xl:text-4xl">{formatNumber(card.amount)}</strong><span className={`text-xs font-semibold ${card.primary ? 'text-white/70' : 'text-on-surface-variant'}`}>VND</span></p>
      <p className={`mt-3 text-xs leading-relaxed ${card.primary ? 'text-white/70' : 'text-on-surface-variant'}`}>{t(card.hint, card.count === undefined ? undefined : { count: card.count })}</p>
    </section>)}
  </div>
}
