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
    onChange(next)
  }

  return (
    <nav aria-label={t('Pages')} className="mt-space-lg flex items-center justify-between gap-space-md">
      <Button
        variant="neutral"
        size="sm"
        icon="chevron_left"
        onClick={() => goTo(pageNumber - 1)}
        disabled={busy || pageNumber <= 1}
      >
        {t('Newer')}
      </Button>
      <span className="text-sm text-on-surface-variant">
        {t('Page {page} of {total}', { page: pageNumber, total: totalPages })}
      </span>
      <Button
        variant="neutral"
        size="sm"
        onClick={() => goTo(pageNumber + 1)}
        disabled={busy || pageNumber >= totalPages}
      >
        {t('Older')}
        <Icon name="chevron_right" className="text-lg" />
      </Button>
    </nav>
  )
}
