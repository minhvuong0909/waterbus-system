import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { ManagedBooking } from '../types'
import { t } from '../../../i18n'

const ACTION_CLASS =
  'inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low'

interface BookingSummaryCardProps {
  booking: ManagedBooking
  /** "Outbound trip" / "Return trip" when the order has both. */
  legLabel?: string
  /** Remembers which ticket the ticket page opens. */
  onOpenTicket: (ticketId: string) => void
}

/** One booking of the order: the trip, then the seats, passengers and fare of all its tickets. */
export default function BookingSummaryCard({ booking, legLabel, onOpenTicket }: BookingSummaryCardProps) {
  const { tickets, bookingCode } = booking
  const [ticket] = tickets
  const statuses = [...new Set(tickets.map((x) => x.status))]

  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-card">
      <div className="flex flex-wrap items-center justify-between gap-space-md">
        <div className="flex items-center gap-3">
          <span className="flex h-12 min-w-[3rem] shrink-0 items-center justify-center whitespace-nowrap rounded-xl bg-deep-river px-2 text-center font-display text-xs font-bold leading-tight text-white">
            {ticket.tripCode}
          </span>
          <div>
            <div className="text-2xs font-semibold uppercase tracking-wider text-teal-flow">
              {legLabel && `${legLabel} · `}
              {t('Booking Code:')} <span className="font-mono normal-case">{bookingCode}</span>
            </div>
            <div className="font-display text-xl font-bold text-deep-river">
              {ticket.departPier} → {ticket.arrivePier}
            </div>
            <div className="text-xs text-on-surface-variant">{ticket.dateLabel}</div>
          </div>
        </div>
        <div className="flex gap-space-sm">
          <Link to={ROUTES.ticketDetail} onClick={() => onOpenTicket(ticket.id)} className={ACTION_CLASS}>
            <Icon name="confirmation_number" className="text-base" />
            {t('View Ticket')}
          </Link>
          <Link to={`${ROUTES.liveTracking}?tripId=${ticket.tripId}`} className={ACTION_CLASS}>
            <Icon name="map" className="text-base" />
            {t('Track Trip')}
          </Link>
        </div>
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-center gap-space-md border-t border-surface-container pt-space-lg md:grid-cols-12">
        <div className="md:col-span-4">
          <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Departure')}
          </div>
          <div className="font-display text-2xl font-bold text-deep-river">{ticket.departPier}</div>
          <div className="font-display text-lg font-semibold text-teal-flow">{ticket.departTime}</div>
        </div>
        <div className="flex flex-col items-center md:col-span-4">
          <span className="text-xs text-teal-flow">
            {t('{durationMins} min crossing', { durationMins: ticket.durationMins })}
          </span>
          <div className="mt-1 flex w-full items-center">
            <span className="h-2.5 w-2.5 rounded-full border-2 border-teal-flow bg-white" />
            <span className="h-1 flex-1 rounded-full bg-gradient-to-r from-teal-flow to-sky-aqua" />
            <span className="h-2.5 w-2.5 rounded-full bg-teal-flow" />
          </div>
        </div>
        <div className="md:col-span-4 md:text-right">
          <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Arrival')}
          </div>
          <div className="font-display text-2xl font-bold text-deep-river">{ticket.arrivePier}</div>
          <div className="font-display text-lg font-semibold text-teal-flow">{ticket.arriveTime}</div>
        </div>
      </div>

      <dl className="mt-space-lg grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md md:grid-cols-4">
        <div>
          <dt className="text-xs text-on-surface-variant">{tickets.length > 1 ? t('Seats') : t('Seat')}</dt>
          <dd className="font-semibold text-deep-river">{tickets.map((x) => x.seat).join(', ')}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Passenger')}</dt>
          <dd className="font-semibold text-deep-river">
            {tickets.length > 1 ? t('{count} passengers', { count: tickets.length }) : t('1 Passenger')}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Fare')}</dt>
          <dd className="font-semibold text-deep-river">
            {formatVnd(tickets.reduce((sum, x) => sum + x.fareVnd, 0))}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Status')}</dt>
          <dd className="font-semibold text-teal-flow">{statuses.join(', ')}</dd>
        </div>
      </dl>
    </div>
  )
}
