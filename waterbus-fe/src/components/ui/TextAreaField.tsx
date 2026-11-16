import type { TextareaHTMLAttributes } from 'react'
import FieldShell from './FieldShell'
import type { FieldShellProps } from './FieldShell'
import { INPUT_CLASS } from './styles'

type ShellOptions = Pick<FieldShellProps, 'hideLabel' | 'fieldClassName'>

/** Ô nhập nhiều dòng, cùng nhãn / gợi ý / lỗi với `TextField`. */
export default function TextAreaField({
  label,
  hint,
  error,
  required,
  hideLabel,
  fieldClassName,
  ...area
}: { label: string; hint?: string; error?: string; required?: boolean } & ShellOptions & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error} required={required} hideLabel={hideLabel} fieldClassName={fieldClassName}>
      {(id) => (
        <textarea id={id} className={INPUT_CLASS} aria-invalid={Boolean(error)} required={required} {...area} />
      )}
    </FieldShell>
  )
}
