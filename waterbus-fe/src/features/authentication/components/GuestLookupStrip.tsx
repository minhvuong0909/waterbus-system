import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function GuestLookupStrip() {
  return (
    <div className="flex flex-col items-center justify-between gap-3 rounded-xl border border-outline-variant/60 bg-surface-container p-4 text-xs text-on-surface-variant sm:flex-row">
      <div className="flex items-center gap-3">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-teal-flow/10 text-teal-flow">
          <Icon name="credit_card" className="text-[18px]" />
        </div>
        <div>
          <span className="font-semibold text-deep-river">
            {t('Traveling today without an account?')}
          </span>
          <span className="block text-on-surface-variant sm:ml-1 sm:inline">
            {t('You can look up your river journey using your booking code.')}
          </span>
        </div>
      </div>
      <Link
        to={ROUTES.ticketDetail}
        className="flex items-center gap-1 whitespace-nowrap font-semibold text-teal-flow hover:text-deep-river"
      >
        <span>{t('Guest Ticket Lookup')}</span>
        <Icon name="arrow_forward" className="text-[16px]" />
      </Link>
    </div>
  )
}
