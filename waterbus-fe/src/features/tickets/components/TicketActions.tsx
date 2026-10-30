import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function TicketActions({ tripId }: { tripId: string }) {
  return (
    <div className="space-y-space-sm rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <Link
        to={`${ROUTES.liveTracking}?tripId=${tripId}`}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
      >
        <Icon name="near_me" className="text-[18px]" />
        {t('Track This Trip')}
      </Link>
      <div className="flex items-center justify-between pt-1 text-sm">
        <Link
          to={ROUTES.myTickets}
          className="flex items-center gap-1 text-on-surface-variant hover:text-deep-river"
        >
          <Icon name="arrow_back" className="text-[16px]" />
          {t('Back to My Tickets')}
        </Link>
        <Link to={ROUTES.manageBooking} className="font-semibold text-teal-flow hover:underline">
          {t('View Booking Details')}
        </Link>
      </div>
    </div>
  )
}
