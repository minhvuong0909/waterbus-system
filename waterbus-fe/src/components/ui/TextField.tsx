import type { InputHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import { INPUT_CLASS } from './styles'

export default function TextField({
  label,
  hint,
  error,
  required,
  ...input
}: { label: string; hint?: string; error?: string; required?: boolean } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error} required={required}>
      {(id) => (
        <input id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} required={required} {...input} />
      )}
    </FieldShell>
  )
}
