import type { ReactNode } from 'react'
import { t } from '../../i18n'

/** Titled white box that groups one part of a page. */
export default function Panel({
  title,
  children,
  className = '',
}: {
  title?: string
  children: ReactNode
  className?: string
}) {
  return (
    <section
      className={`rounded-2xl border border-outline-variant/30 bg-white p-4 shadow-[0_2px_8px_rgba(13,37,56,0.025)] sm:p-5 ${className}`.trim()}
    >
      {title && <h2 className="mb-4 text-base font-semibold text-deep-river">{t(title)}</h2>}
      {children}
    </section>
  )
}
