import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import type { OpenAlertSummary } from '../hooks/useOpenAlertSummary'

/**
 * Strip shown at the top of every admin page while alerts are open, so the admin sees a weather or emergency alert
 * without opening the Incidents page. Hidden on that page itself, which lists the same alerts in full.
 */
export default function AdminAlertBar({ summary, pathname }: { summary: OpenAlertSummary | null; pathname: string }) {
  if (!summary || pathname.startsWith(ROUTES.adminIncidents)) return null
  const stop = summary.level === 2
  return (
    <div
      role="alert"
      className={`mb-4 flex flex-wrap items-center gap-x-4 gap-y-2 rounded-xl border border-l-4 bg-white px-4 py-3 text-sm shadow-sm ${
        stop ? 'border-coral-glow' : 'border-signal-amber'
      }`}
    >
      <Icon name="warning" className={`text-xl ${stop ? 'text-coral-glow' : 'text-signal-amber'}`} />
      <div className="min-w-0 flex-1 font-semibold text-deep-river">
        <span>
          {t('Open alerts')}: {summary.open}
        </span>
        {summary.waiting > 0 && (
          <span className="ml-3 font-normal text-on-surface-variant">
            {t('Trips waiting for a decision')}: {summary.waiting}
          </span>
        )}
      </div>
      <Link
        to={ROUTES.adminIncidents}
        className="rounded-full bg-deep-river px-4 py-1.5 text-xs font-semibold text-white hover:bg-primary-container"
      >
        {t('View and handle')}
      </Link>
    </div>
  )
}
