import type { ReactNode } from 'react'
import Button from './Button'
import { t } from '../../i18n'

/** Horizontal row of filter controls (SelectField/TextField) above a DataTable, with an optional reset. */
export default function FilterBar({
  children,
  onReset,
  resetDisabled = false,
}: {
  children: ReactNode
  onReset?: () => void
  resetDisabled?: boolean
}) {
  return (
    <div role="group" aria-label={t('Filters')} className="mb-5 flex flex-wrap items-end gap-4 rounded-2xl border border-outline-variant/40 bg-white p-4 shadow-card sm:p-5 [&>div]:min-w-[160px] [&>div]:flex-1">
      <span aria-hidden="true" className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-teal-flow/10 text-teal-flow">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
          <path d="M4 6h16M4 12h16M4 18h16" />
          <circle cx="9" cy="6" r="2" fill="currentColor" stroke="none" />
          <circle cx="15" cy="12" r="2" fill="currentColor" stroke="none" />
          <circle cx="9" cy="18" r="2" fill="currentColor" stroke="none" />
        </svg>
      </span>
      {children}
      {onReset && (
        <Button variant="neutral" size="sm" onClick={onReset} disabled={resetDisabled} className="rounded-xl">
          <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 10a9 9 0 1 1 2 8M3 4v6h6" />
          </svg>
          {t('Clear filters')}
        </Button>
      )}
    </div>
  )
}
