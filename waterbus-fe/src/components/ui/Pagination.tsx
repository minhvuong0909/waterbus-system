import Icon from './Icon'
import { t } from '../../i18n'

const ARROW =
  'flex h-9 w-9 items-center justify-center rounded-xl border border-outline-variant/70 bg-white text-deep-river transition-colors hover:bg-mist disabled:cursor-not-allowed disabled:opacity-40'

/** Chuyển trang gọn cho mọi danh sách `Paged<T>`: ‹ Trang x/y ›. Ẩn khi chỉ có một trang. */
export default function Pagination({
  pageNumber,
  totalPages,
  onChange,
  busy = false,
}: {
  pageNumber: number
  totalPages: number
  onChange: (next: number) => void
  busy?: boolean
}) {
  if (totalPages <= 1) return null

  const goTo = (next: number) => {
    if (!busy && next >= 1 && next <= totalPages) onChange(next)
  }

  return (
    <nav aria-label={t('Pages')} className="flex items-center justify-end gap-2">
      <button
        type="button"
        aria-label={t('Previous page')}
        className={ARROW}
        onClick={() => goTo(pageNumber - 1)}
        disabled={busy || pageNumber <= 1}
      >
        <Icon name="chevron_left" className="text-lg" />
      </button>
      <span className="min-w-[4.5rem] text-center text-sm tabular-nums text-on-surface-variant" aria-live="polite">
        {t('Page {page} of {total}', { page: pageNumber, total: totalPages })}
      </span>
      <button
        type="button"
        aria-label={t('Next page')}
        className={ARROW}
        onClick={() => goTo(pageNumber + 1)}
        disabled={busy || pageNumber >= totalPages}
      >
        <Icon name="chevron_right" className="text-lg" />
      </button>
    </nav>
  )
}
