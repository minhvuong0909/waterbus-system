import type { ReactNode } from 'react'

/** Icon-in-circle + title + optional subtitle heading used on booking cards. */
export default function CardHeading({
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
    <div className="mb-space-md flex flex-wrap items-start justify-between gap-3">
      <div className="flex min-w-0 items-center gap-space-sm">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-secondary-container text-teal-flow">
          <span aria-hidden="true" className="material-symbols-outlined text-2xl">
            {icon}
          </span>
        </div>
        <div>
          <h2 className="font-display text-headline-sm text-deep-river">{title}</h2>
          {subtitle && <p className="text-xs text-on-surface-variant">{subtitle}</p>}
        </div>
      </div>
      {aside}
    </div>
  )
}
