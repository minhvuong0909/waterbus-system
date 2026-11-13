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
    <div className="mb-4 flex flex-wrap items-end gap-3 rounded-2xl border border-outline-variant/30 bg-white p-4 shadow-card">
      {children}
      {onReset && (
        <Button variant="text" size="sm" onClick={onReset} disabled={resetDisabled} className="mb-0.5">
          {t('Clear filters')}
        </Button>
      )}
    </div>
  )
}
