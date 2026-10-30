import { useId } from 'react'
import type { ReactNode } from 'react'
import { t } from '../../i18n'

export interface FieldShellProps {
  label: string
  hint?: string
  error?: string
  children: (id: string) => ReactNode
}

/** Label, hint and error around a form control; gives the control its id. */
export default function FieldShell({ label, hint, error, children }: FieldShellProps) {
  const id = useId()
  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-semibold text-on-surface-variant">
        {t(label)}
      </label>
      {children(id)}
      {hint && !error && <p className="mt-1 text-2xs text-on-surface-variant">{t(hint)}</p>}
      {error && (
        <p role="alert" className="mt-1 text-2xs text-coral-glow">
          {error}
        </p>
      )}
    </div>
  )
}
