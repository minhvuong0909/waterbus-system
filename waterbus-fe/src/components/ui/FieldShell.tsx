import { useId } from 'react'
import type { ReactNode } from 'react'
import { t } from '../../i18n'

/** Shared look of the form inputs. */
export const inputClass =
  'min-h-[42px] w-full rounded-xl border border-outline-variant/70 bg-white px-3 py-2 text-sm text-deep-river transition-colors focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15 disabled:bg-surface-container-low'

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
      {hint && !error && <p className="mt-1 text-[11px] text-on-surface-variant">{t(hint)}</p>}
      {error && (
        <p role="alert" className="mt-1 text-[11px] text-coral-glow">
          {error}
        </p>
      )}
    </div>
  )
}
