import type { SelectHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import { INPUT_CLASS } from './styles'

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
        <select id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} {...select}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}
