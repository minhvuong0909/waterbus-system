import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function GuestBookingNote() {
  return (
    <div className="flex flex-col items-stretch justify-between gap-4 rounded-2xl border border-teal-flow/15 bg-white p-4 text-sm text-on-surface-variant sm:flex-row sm:items-center sm:p-5">
      <div className="flex items-center gap-3">
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-teal-flow/10 text-teal-flow">
          <Icon name="confirmation_number" className="text-[18px]" />
        </div>
        <div>
          <span className="font-semibold text-deep-river">
            {t('No account needed to travel')}
          </span>
          <span className="mt-1 block text-xs leading-relaxed text-on-surface-variant">
            {t('Book as a guest and pay online; your QR tickets appear right after payment.')}
          </span>
        </div>
      </div>
      <Link
        to={ROUTES.search}
        className="flex min-h-[44px] shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-xl bg-teal-flow/10 px-4 py-2 text-sm font-semibold text-teal-flow transition-colors hover:bg-teal-flow/15"
      >
        <span>{t('Book a ticket')}</span>
        <Icon name="arrow_forward" className="text-[16px]" />
      </Link>
    </div>
  )
}
