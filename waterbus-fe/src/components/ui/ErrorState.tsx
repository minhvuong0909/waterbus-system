import { Link } from 'react-router-dom'
import { ROUTES } from '../../routes/routes'
import Icon from './Icon'
import { t } from '../../i18n'

interface ErrorStateProps {
  onRetry?: () => void
  title?: string
  message?: string
  /** Smaller, inline version for a card or panel instead of a whole page. */
  compact?: boolean
}

/** Shown when loading data fails. Offers a retry and a way back home. */
export default function ErrorState({
  onRetry,
  title = t('We couldn’t load this page'),
  message = t('Something went wrong while loading the data. Check your connection and try again.'),
  compact = false,
}: ErrorStateProps) {
  if (compact) {
    return (
      <div
        role="alert"
        className="flex items-center justify-between gap-space-md rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-space-md py-space-sm text-sm text-deep-river"
      >
        <span className="flex items-center gap-2">
          <Icon name="error" className="text-[18px] text-coral-glow" />
          {message}
        </span>
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            className="shrink-0 rounded-lg bg-white px-3 py-1.5 text-xs font-semibold text-teal-flow shadow-sm hover:bg-surface-container-low"
          >
            {t('Try again')}
          </button>
        )}
      </div>
    )
  }

  return (
    <div
      role="alert"
      className="mx-auto flex min-h-[60vh] max-w-lg flex-col items-center justify-center gap-space-md px-margin py-space-3xl text-center"
    >
      <span className="flex h-16 w-16 items-center justify-center rounded-full bg-coral-glow/15 text-coral-glow">
        <Icon name="cloud_off" className="text-[34px]" />
      </span>
      <h1 className="font-headline-md text-headline-md text-deep-river">{title}</h1>
      <p className="text-body-md text-on-surface-variant">{message}</p>
      <div className="flex flex-wrap justify-center gap-space-sm pt-space-xs">
        {onRetry && (
          <button
            type="button"
            onClick={onRetry}
            className="inline-flex items-center gap-2 rounded-full bg-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-on-primary shadow-sm transition-colors hover:bg-secondary"
          >
            <Icon name="refresh" className="text-[18px]" />
            {t('Try again')}
          </button>
        )}
        <Link
          to={ROUTES.home}
          className="inline-flex items-center rounded-full border border-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-teal-flow transition-colors hover:bg-sand-light"
        >
          {t('Back to home')}
        </Link>
      </div>
    </div>
  )
}
