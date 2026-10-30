import type { ReactNode } from 'react'
import { t } from '../../i18n'

/** Title of a back-office page, with an optional description and actions on the right. */
export default function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: ReactNode
}) {
  return (
    <div className="mb-5 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="font-headline-md text-2xl font-bold leading-tight tracking-tight text-deep-river sm:text-[28px]">
          {t(title)}
        </h1>
        {description && <p className="mt-1 text-sm text-on-surface-variant">{t(description)}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}
