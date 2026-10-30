import type { InputHTMLAttributes } from 'react'
import FieldShell, { inputClass } from './FieldShell'

export default function TextField({
  label,
  hint,
  error,
  ...input
}: { label: string; hint?: string; error?: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(id) => <input id={id} className={inputClass} aria-invalid={Boolean(error)} {...input} />}
    </FieldShell>
  )
}
