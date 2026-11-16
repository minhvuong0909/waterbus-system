import { useId } from 'react'
import type { ReactNode } from 'react'
import { t } from '../../i18n'
import { REQUIRED_LABEL_CLASS } from './styles'

export interface FieldShellProps {
  label: string
  hint?: string
  error?: string
  /** Ô bắt buộc theo backend: nhãn có dấu (*). */
  required?: boolean
  /** Ẩn nhãn khỏi mắt nhìn nhưng vẫn đọc được bằng trình đọc màn hình (ô nằm gọn trong một hàng). */
  hideLabel?: boolean
  /** Lớp cho khung bao quanh (không phải ô nhập). */
  fieldClassName?: string
  children: (id: string) => ReactNode
}

/** Nhãn, gợi ý và lỗi bao quanh một ô nhập; cấp id cho ô đó. */
export default function FieldShell({ label, hint, error, required, hideLabel, fieldClassName, children }: FieldShellProps) {
  const id = useId()
  return (
    <div className={fieldClassName}>
      <label
        htmlFor={id}
        className={
          hideLabel
            ? 'sr-only'
            : `mb-1 block text-xs font-semibold text-on-surface-variant ${required ? REQUIRED_LABEL_CLASS : ''}`
        }
      >
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
