import Icon from './Icon'
import { t } from '../../i18n'

const ARROW_BUTTON =
  'flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-outline-variant/40 bg-white text-deep-river transition-colors hover:bg-mist disabled:pointer-events-none disabled:opacity-40'

/** Shared page-forward/page-back control for any `Paged<T>` list. Numbered pages only: no separate "Page X of Y" text, which only duplicated them. */
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
  const pages = Array.from(new Set([1, pageNumber - 1, pageNumber, pageNumber + 1, totalPages]))
    .filter((page) => page >= 1 && page <= totalPages)
    .sort((a, b) => a - b)

  return (
    <nav aria-label={t('Pages')} className="flex flex-wrap items-center gap-1.5 py-2">
      <button type="button" aria-label={t('Previous page')} disabled={busy || pageNumber <= 1}
        onClick={() => goTo(pageNumber - 1)} className={ARROW_BUTTON}>
        <Icon name="chevron_left" className="text-lg" />
      </button>
      {pages.map((page, index) => (
        <span key={page} className="inline-flex items-center gap-1.5">
          {index > 0 && page - pages[index - 1] > 1 && (
            <span aria-hidden="true" className="px-0.5 text-sm text-on-surface-variant">
              …
            </span>
          )}
          <button type="button" aria-label={t('Go to page {page}', { page })} aria-current={page === pageNumber ? 'page' : undefined}
            disabled={busy} onClick={() => goTo(page)}
            className={`flex h-11 min-w-[44px] items-center justify-center rounded-xl px-2 text-sm font-semibold transition-colors disabled:opacity-50 ${page === pageNumber ? 'bg-teal-flow text-white shadow-sm' : 'border border-outline-variant/40 bg-white text-deep-river hover:bg-mist'}`}>
            {page}
          </button>
        </span>
      ))}
      <button type="button" aria-label={t('Next page')} disabled={busy || pageNumber >= totalPages}
        onClick={() => goTo(pageNumber + 1)} className={ARROW_BUTTON}>
        <Icon name="chevron_right" className="text-lg" />
      </button>
    </nav>
  )
}
