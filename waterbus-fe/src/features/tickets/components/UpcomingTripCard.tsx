import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

export default function UpcomingTripCard({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-card">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs">
          <span className="rounded bg-mist px-2 py-1 font-semibold text-on-surface-variant">
            {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
          </span>
          <span className="text-on-surface-variant">• {ticket.routeTag}</span>
        </div>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-2xs font-semibold text-on-secondary-container">
          {ticket.status}
        </span>
      </div>
      <div className="mt-space-md grid grid-cols-3 items-center">
        <div>
          <div className="font-display text-2xl font-bold text-deep-river">
            {ticket.departTime}
          </div>
          <div className="text-sm font-semibold text-deep-river">{ticket.departPier}</div>
          <div className="text-xs text-on-surface-variant">{ticket.departDistrict}</div>
        </div>
        <div className="flex flex-col items-center text-xs text-teal-flow">
          {t('{durationMins} min', { durationMins: ticket.durationMins })}
          <Icon name="arrow_forward" className="text-xl" />
        </div>
        <div className="text-right">
          <div className="font-display text-2xl font-bold text-deep-river">
            {ticket.arriveTime}
          </div>
          <div className="text-sm font-semibold text-deep-river">{ticket.arrivePier}</div>
          <div className="text-xs text-on-surface-variant">{ticket.arriveDistrict}</div>
        </div>
      </div>
      <div className="mt-space-md grid grid-cols-3 gap-2 text-xs">
        <div>
          <div className="text-on-surface-variant">{t('Date')}</div>
          <div className="font-semibold text-deep-river">{ticket.dateLabel}</div>
        </div>
        <div>
          <div className="text-on-surface-variant">{t('Seat / Passenger')}</div>
          <div className="font-semibold text-deep-river">
            {ticket.seat} • {ticket.passenger}
          </div>
        </div>
        <div className="text-right">
          <div className="text-on-surface-variant">{t('Fare')}</div>
          <div className="font-semibold text-deep-river">{formatVnd(ticket.fareVnd)}</div>
        </div>
      </div>
      <div className="mt-space-md flex items-center justify-between border-t border-surface-container pt-space-md">
        <Link
          to={ROUTES.manageBooking}
          className="text-xs text-on-surface-variant hover:text-deep-river"
        >
          {t('Trip Details')}
        </Link>
        <Link
          to={ROUTES.ticketDetail}
          onClick={() => onOpen(ticket.id)}
          className="inline-flex items-center gap-1.5 rounded-lg bg-surface-container px-3 py-2 text-xs font-semibold text-teal-flow transition-colors hover:bg-surface-container-high"
        >
          <Icon name="qr_code" className="text-base" />
          {t('View Ticket')}
        </Link>
      </div>
    </div>
  )
}
