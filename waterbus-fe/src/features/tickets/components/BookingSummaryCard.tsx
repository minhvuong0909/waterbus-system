import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

export default function BookingSummaryCard({ ticket }: { ticket: Ticket }) {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-card">
      <div className="flex flex-wrap items-center justify-between gap-space-md">
        <div className="flex items-center gap-3">
          <span className="flex h-12 min-w-[3rem] shrink-0 items-center justify-center whitespace-nowrap rounded-xl bg-deep-river px-2 text-center font-display text-xs font-bold leading-tight text-white">
            {ticket.tripCode}
          </span>
          <div>
            <div className="text-2xs font-semibold uppercase tracking-wider text-teal-flow">
              {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
            </div>
            <div className="font-display text-xl font-bold text-deep-river">
              {ticket.departPier} → {ticket.arrivePier}
            </div>
          </div>
        </div>
        <div className="flex gap-space-sm">
          <Link
            to={ROUTES.ticketDetail}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
            <Icon name="confirmation_number" className="text-base" />
            {t('View Ticket')}
          </Link>
          <Link
            to={`${ROUTES.liveTracking}?tripId=${ticket.tripId}`}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
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
          <div className="font-display text-2xl font-bold text-deep-river">
            {ticket.departPier}
          </div>
          <div className="font-display text-lg font-semibold text-teal-flow">
            {ticket.departTime}
          </div>
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
          <div className="font-display text-2xl font-bold text-deep-river">
            {ticket.arrivePier}
          </div>
          <div className="font-display text-lg font-semibold text-teal-flow">
            {ticket.arriveTime}
          </div>
        </div>
      </div>

      <dl className="mt-space-lg grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md md:grid-cols-4">
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Seat')}</dt>
          <dd className="font-semibold text-deep-river">{ticket.seat}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Passenger')}</dt>
          <dd className="font-semibold text-deep-river">{t('1 Passenger')}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Fare')}</dt>
          <dd className="font-semibold text-deep-river">{formatVnd(ticket.fareVnd)}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Status')}</dt>
          <dd className="font-semibold text-teal-flow">{ticket.status}</dd>
        </div>
      </dl>
    </div>
  )
}
