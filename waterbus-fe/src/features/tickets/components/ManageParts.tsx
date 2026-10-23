import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

export function BookingSummaryCard({ ticket }: { ticket: Ticket }) {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <div className="flex flex-wrap items-center justify-between gap-space-md">
        <div className="flex items-center gap-3">
          <span className="flex h-12 min-w-[3rem] shrink-0 items-center justify-center whitespace-nowrap rounded-xl bg-deep-river px-2 text-center font-headline-sm text-xs font-bold leading-tight text-white">
            {ticket.tripCode}
          </span>
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-wider text-teal-flow">
              {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
            </div>
            <div className="font-headline-sm text-xl font-bold text-deep-river">
              {ticket.departPier} → {ticket.arrivePier}
            </div>
          </div>
        </div>
        <div className="flex gap-space-sm">
          <Link
            to={ROUTES.ticketDetail}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
            <Icon name="confirmation_number" className="text-[16px]" />
            {t('View Ticket')}
          </Link>
          <Link
            to={ROUTES.liveTracking}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
            <Icon name="map" className="text-[16px]" />
            {t('Track Trip')}
          </Link>
        </div>
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-center gap-space-md border-t border-surface-container pt-space-lg md:grid-cols-12">
        <div className="md:col-span-4">
          <div className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Departure')}
          </div>
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
            {ticket.departPier}
          </div>
          <div className="font-headline-sm text-lg font-semibold text-teal-flow">
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
          <div className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Arrival')}
          </div>
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
            {ticket.arrivePier}
          </div>
          <div className="font-headline-sm text-lg font-semibold text-teal-flow">
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

export function OperatingBanner({ vessel }: { vessel: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-teal-flow/25 bg-secondary-container/40 px-space-md py-space-md text-sm">
      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-teal-flow text-on-primary">
        <Icon name="check" className="text-[18px]" />
      </span>
      <span>
        <b className="text-deep-river">{t('Trip Operating Normally.')}</b>{' '}
        <span className="text-teal-flow">
          {t('Vessel {vessel} is operating on schedule.', { vessel })}
        </span>
      </span>
    </div>
  )
}

/**
 * What happens to the money if the trip does not run (BR v2.0 §10). Passengers do not file refund
 * requests: when a trip is cancelled or terminated early, the system refunds the paid amount of
 * the affected tickets to the original payment method.
 */
export function RefundPolicyCard() {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <h2 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
        <Icon name="payments" className="text-[22px] text-teal-flow" />
        {t('Refunds')}
      </h2>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t(
          'If this trip is cancelled or ends early before you reach your stop, the amount you paid for the affected tickets is refunded to your original payment method. Any refund appears here with its status.',
        )}
      </p>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t('Tickets of passengers who miss check-in are not refunded.')}
      </p>
    </div>
  )
}
