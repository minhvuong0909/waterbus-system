import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { t } from '../../i18n'
import Icon from './Icon'

/** Hộp thoại có tiêu đề, đóng bằng Escape, nền mờ hoặc nút đóng. */
export default function Modal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  const titleId = useId()
  const dialogRef = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  // Đưa focus vào hộp thoại khi mở và trả lại cho nút đã mở nó.
  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    dialogRef.current?.focus()
    return () => opener?.focus?.()
  }, [])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label={t('Close')}
        onClick={onClose}
        className="absolute inset-0 cursor-default bg-deep-river/40"
      />
      <div
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="relative max-h-full w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-5 shadow-xl focus:outline-none"
      >
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 id={titleId} className="font-display text-lg font-bold text-deep-river">
            {t(title)}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('Close')}
            className="flex h-8 w-8 items-center justify-center rounded-full hover:bg-surface-container"
          >
            <Icon name="close" className="text-lg" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
