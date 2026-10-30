import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import { alertMetrics, alertTone, formatKmh, pendingCountOf, timeAgo, titleOf } from '../alertUtils'
import type { WeatherAlert } from '../types'

/** Compact strip for the live radar: how serious, where, and how many trips still need a decision. */
export default function AlertBanner({ alert }: { alert: WeatherAlert }) {
  const tone = alertTone(alert)
  const { windKmh, gustKmh } = alertMetrics(alert)
  const pending = pendingCountOf(alert)
  return (
    <div
      role="alert"
      className={`flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-l-4 border-outline-variant/40 bg-white px-4 py-3 text-sm shadow-sm ${tone.accent}`}
    >
      <span className={`flex h-8 w-8 items-center justify-center rounded-lg ${tone.badge}`}>
        <Icon name={tone.icon} className="text-lg" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className={`rounded-full px-2 py-0.5 text-2xs font-bold uppercase tracking-wide ${tone.badge}`}>
            {tone.label}
          </span>
          <span className="font-semibold text-deep-river">{titleOf(alert)}</span>
        </div>
        <div className="mt-0.5 text-xs text-on-surface-variant">
          {[
            windKmh !== undefined ? `${t('Wind')} ${formatKmh(windKmh)}` : null,
            gustKmh !== undefined ? `${t('Gusts')} ${formatKmh(gustKmh)}` : null,
            timeAgo(alert.issuedAt),
          ]
            .filter(Boolean)
            .join(' · ')}
        </div>
      </div>
      {pending > 0 && (
        <span className="rounded-full bg-coral-glow/15 px-2.5 py-1 text-xs font-semibold text-coral-glow">
          {t('{count} trips need a decision', { count: pending })}
        </span>
      )}
      <Link
        to={ROUTES.adminIncidents}
        className="inline-flex min-h-[36px] items-center gap-1 rounded-full bg-deep-river px-3 text-xs font-semibold text-white hover:opacity-90"
      >
        {t('Review alert')}
        <Icon name="arrow_forward" className="text-base" />
      </Link>
    </div>
  )
}
