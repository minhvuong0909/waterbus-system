import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function GuestLookupStrip() {
  return (
    <div className="flex flex-col items-stretch justify-between gap-4 rounded-2xl border border-teal-flow/15 bg-white p-4 text-sm text-on-surface-variant sm:flex-row sm:items-center sm:p-5">
      <div className="flex items-center gap-3">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-teal-flow/10 text-teal-flow">
          <Icon name="credit_card" className="text-[18px]" />
        </div>
        <div>
          <span className="font-semibold text-deep-river">
            {t('Traveling today without an account?')}
          </span>
          <span className="mt-1 block text-xs leading-relaxed text-on-surface-variant">
            {t('You can look up your river journey using your booking code.')}
          </span>
        </div>
      </div>
      <Link
        to={ROUTES.ticketDetail}
        className="flex min-h-[44px] shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-xl bg-teal-flow/10 px-4 py-2 text-sm font-semibold text-teal-flow transition-colors hover:bg-teal-flow/15"
      >
        <span>{t('Guest Ticket Lookup')}</span>
        <Icon name="arrow_forward" className="text-[16px]" />
      </Link>
    </div>
  )
}
