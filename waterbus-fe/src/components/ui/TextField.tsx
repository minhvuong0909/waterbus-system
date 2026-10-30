import type { InputHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import { INPUT_CLASS } from './styles'

export default function TextField({
  label,
  hint,
  error,
  ...input
}: { label: string; hint?: string; error?: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(id) => <input id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} {...input} />}
    </FieldShell>
  )
}
