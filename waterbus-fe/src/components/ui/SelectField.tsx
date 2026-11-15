import type { SelectHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import { INPUT_CLASS } from './styles'

export default function SelectField({
  label,
  hint,
  error,
  required,
  children,
  ...select
}: { label: string; hint?: string; error?: string; required?: boolean } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error} required={required}>
      {(id) => (
        <select id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} required={required} {...select}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}
