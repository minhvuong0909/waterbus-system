import type { ReactNode } from 'react'

/** "Label ........ value" row used in every fare/summary card. */
export default function SummaryRow({
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
