import type { ReactNode } from 'react'
import { SURFACE_CLASS } from './styles'
import { t } from '../../i18n'

/** Hàng bộ lọc (SelectField/TextField) phía trên một DataTable. */
export default function FilterBar({ children }: { children: ReactNode }) {
  return (
    <div
      role="group"
      aria-label={t('Filters')}
      className={`${SURFACE_CLASS} mb-4 flex flex-wrap items-end gap-4 p-4 [&>div]:min-w-[160px] [&>div]:flex-1`}
    >
      {children}
    </div>
  )
}
