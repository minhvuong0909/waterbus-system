import type { SelectHTMLAttributes } from 'react'
import FieldShell, { inputClass } from './FieldShell'

export default function SelectField({
  label,
  hint,
  error,
  children,
  ...select
}: { label: string; hint?: string; error?: string } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(id) => (
        <select id={id} className={inputClass} aria-invalid={Boolean(error)} {...select}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}
