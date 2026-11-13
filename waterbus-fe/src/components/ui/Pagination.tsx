import Button from './Button'
import Icon from './Icon'
import { t } from '../../i18n'

/** Shared page-forward/page-back control for any `Paged<T>` list. Mirrors OrderHistoryPage's own. */
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
    <nav aria-label={t('Pages')} className="flex flex-wrap items-center justify-end gap-2 py-2">
      <Button
        variant="neutral"
        size="sm"
        icon="chevron_left"
        onClick={() => goTo(pageNumber - 1)}
        disabled={busy || pageNumber <= 1}
      >
        {t('Previous page')}
      </Button>
      <span className="text-sm text-on-surface-variant">
        {t('Page {page} of {total}', { page: pageNumber, total: totalPages })}
      </span>
      {Array.from(new Set([1, pageNumber - 1, pageNumber, pageNumber + 1, totalPages]))
        .filter((page) => page >= 1 && page <= totalPages).sort((a, b) => a - b)
        .map((page, index, pages) => (
          <span key={page} className="inline-flex items-center gap-2">
            {index > 0 && page - pages[index - 1] > 1 && <span aria-hidden="true">…</span>}
            <button type="button" aria-label={t('Go to page {page}', { page })} aria-current={page === pageNumber ? 'page' : undefined}
              disabled={busy} onClick={() => goTo(page)}
              className={`flex h-11 min-w-[44px] items-center justify-center rounded-xl px-2 text-sm font-semibold transition-colors disabled:opacity-50 ${page === pageNumber ? 'bg-teal-flow text-white shadow-sm' : 'border border-outline-variant/40 bg-white text-deep-river hover:bg-mist'}`}>
              {page}
            </button>
          </span>
        ))}
      <Button
        variant="neutral"
        size="sm"
        onClick={() => goTo(pageNumber + 1)}
        disabled={busy || pageNumber >= totalPages}
      >
        {t('Next page')}
        <Icon name="chevron_right" className="text-lg" />
      </Button>
    </nav>
  )
}
