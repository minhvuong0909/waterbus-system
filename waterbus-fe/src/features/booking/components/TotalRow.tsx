import { formatNumber } from '../../../utils/format'
import { t } from '../../../i18n'

/** Big "Total ... 15,000 VND" line with a caption. */
export default function TotalRow({
  label,
  caption,
  amountVnd,
  stacked,
}: {
  label: string
  caption?: string
  amountVnd: number
  /** Puts the VND unit under the number, as on the checkout order summary. */
  stacked?: boolean
}) {
  const amount = formatNumber(amountVnd)
  return (
    <div className="flex items-end justify-between gap-3 py-space-sm">
      <div className="min-w-0">
        <div className="font-headline-sm text-base font-bold text-deep-river">{label}</div>
        {caption && <div className="text-xs text-on-surface-variant">{caption}</div>}
      </div>
      {stacked ? (
        <div className="text-right">
          <div className="font-numeric-lg text-numeric-lg font-bold leading-none text-deep-river">
            {amount}
          </div>
          <div className="text-xs font-semibold text-teal-flow">{t('VND')}</div>
        </div>
      ) : (
        // The amount and its unit stay together; the caption on the left wraps instead.
        <div className="shrink-0 whitespace-nowrap font-numeric-lg text-numeric-lg font-bold leading-none text-deep-river">
          {t('{amount} VND', { amount })}
        </div>
      )}
    </div>
  )
}
