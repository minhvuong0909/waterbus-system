import type { ReactNode } from 'react'
import { t } from '../../i18n'

/** Tiêu đề một phần của trang (trong Panel hoặc phía trên một bảng), kèm mô tả và nút ở bên phải. */
export default function SectionHeader({
  title,
  description,
  actions,
  className = 'mb-4',
}: {
  /** Chuỗi nguồn tiếng Anh. */
  title: string
  /** Chuỗi đã dịch hoặc chuỗi nguồn tiếng Anh. */
  description?: ReactNode
  actions?: ReactNode
  className?: string
}) {
  return (
    <div className={`flex flex-wrap items-start justify-between gap-x-4 gap-y-2 ${className}`.trim()}>
      <div className="min-w-0">
        <h2 className="text-base font-semibold text-deep-river">{t(title)}</h2>
        {description && (
          <p className="mt-0.5 text-sm text-on-surface-variant">
            {typeof description === 'string' ? t(description) : description}
          </p>
        )}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}
