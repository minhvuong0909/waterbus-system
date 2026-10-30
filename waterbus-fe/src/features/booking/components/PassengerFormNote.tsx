import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

/** Closing line under the passenger cards. */
export default function PassengerFormNote() {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl bg-white px-space-lg py-space-md text-xs text-on-surface-variant shadow-card">
      <span className="flex items-center gap-2">
        <Icon name="info" className="text-lg text-teal-flow" />
        {t('Each passenger gets a ticket in their own name.')}
      </span>
      <Link
        to={ROUTES.seatSelection}
        className="whitespace-nowrap font-semibold text-teal-flow hover:underline"
      >
        {t('Return to seat map')}
      </Link>
    </div>
  )
}
