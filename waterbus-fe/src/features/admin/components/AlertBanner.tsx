import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import { alertTone, pendingCountOf, timeAgo, titleOf } from '../utils/alertUtils'
import type { IncidentAlert } from '../../operations/types'

/** Dải gọn cho radar: mức độ, ở đâu và còn bao nhiêu chuyến chờ quyết định. */
export default function AlertBanner({ alert }: { alert: IncidentAlert }) {
  const tone = alertTone(alert)
  const pending = pendingCountOf(alert)
  return (
    <div
      role="alert"
      className={`flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-l-4 border-outline-variant/30 bg-white px-4 py-3 text-sm shadow-sm ${tone.accent}`}
    >
      <span className={`flex h-8 w-8 items-center justify-center rounded-xl ${tone.badge}`}>
        <Icon name={tone.icon} className="text-lg" />
      </span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className={`rounded-full px-2 py-0.5 text-2xs font-bold ${tone.badge}`}>
            {tone.label}
          </span>
          <span className="font-semibold text-deep-river">{titleOf(alert)}</span>
        </div>
        <div className="mt-0.5 line-clamp-2 text-xs text-on-surface-variant">
          {[timeAgo(alert.issuedAt), alert.message].filter(Boolean).join(' · ')}
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
