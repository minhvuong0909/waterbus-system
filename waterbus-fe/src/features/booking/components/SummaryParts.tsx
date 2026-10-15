import type { ReactNode } from 'react'
import { formatNumber, formatVnd } from '../../../utils/format'
import { t } from '../../../i18n'

/** "Label ........ value" row used in every fare/summary card. */
export function SummaryRow({
  label,
  value,
  accent,
}: {
  label: string
  value: ReactNode
  accent?: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-3 text-sm">
      <span className="text-on-surface-variant">{label}</span>
      <span className={accent ? 'font-medium text-teal-flow' : 'text-deep-river'}>{value}</span>
    </div>
  )
}

/** Seat fee line: "+VND 9,000" for a VIP seat, or `includedText` when the seat is free to reserve. */
export function SeatFeeRow({
  label,
  seatFeeVnd,
  includedText,
}: {
  label: string
  seatFeeVnd: number
  includedText: string
}) {
  return (
    <SummaryRow
      label={label}
      value={seatFeeVnd > 0 ? `+${formatVnd(seatFeeVnd)}` : includedText}
      accent
    />
  )
}

/** Big "Total ... 15,000 VND" line with a caption. */
export function TotalRow({
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
      <div>
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
        <div className="font-numeric-lg text-numeric-lg font-bold leading-none text-deep-river">
          {t('{amount} VND', { amount })}
        </div>
      )}
    </div>
  )
}

/** White rounded card shell used by the booking pages. */
export function BookingCard({
  className = '',
  children,
}: {
  className?: string
  children: ReactNode
}) {
  return (
    <section
      className={`rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)] ${className}`.trim()}
    >
      {children}
    </section>
  )
}

/** Icon-in-circle + title + optional subtitle heading used on booking cards. */
export function CardHeading({
  icon,
  title,
  subtitle,
  aside,
}: {
  icon: string
  title: string
  subtitle?: string
  aside?: ReactNode
}) {
  return (
    <div className="mb-space-md flex items-start justify-between gap-3">
      <div className="flex items-center gap-space-sm">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-secondary-container text-teal-flow">
          <span aria-hidden="true" className="material-symbols-outlined text-[22px]">
            {icon}
          </span>
        </div>
        <div>
          <h2 className="font-headline-md text-headline-sm text-deep-river">{title}</h2>
          {subtitle && <p className="text-xs text-on-surface-variant">{subtitle}</p>}
        </div>
      </div>
      {aside}
    </div>
  )
}
