import type { InputHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import type { FieldShellProps } from './FieldShell'
import { INPUT_CLASS } from './styles'

type ShellOptions = Pick<FieldShellProps, 'hideLabel' | 'fieldClassName'>

export default function TextField({
  label,
  hint,
  error,
  required,
  hideLabel,
  fieldClassName,
  ...input
}: { label: string; hint?: string; error?: string; required?: boolean } & ShellOptions & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error} required={required} hideLabel={hideLabel} fieldClassName={fieldClassName}>
      {(id) => (
        <input id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} required={required} {...input} />
      )}
    </FieldShell>
  )
}
