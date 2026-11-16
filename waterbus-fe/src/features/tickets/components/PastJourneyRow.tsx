import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
import { bookingStatusLabel } from '../utils'
import { t } from '../../../i18n'

/** Biểu tượng, màu và ghi chú tiền của một vé đã đóng, theo vòng đời và check-in (BR-RF). */
function closedTicketLook(ticket: Ticket) {
  const refund = ticket.refund
  const amount = refund ? formatVnd(refund.amountVnd) : ''
  if (ticket.lifecycle === 'Cancelled' || ticket.lifecycle === 'Terminated') {
    return {
      icon: 'event_busy',
      tone: 'bg-coral-glow/15 text-coral-glow',
      badge: 'bg-coral-glow/15 text-coral-glow',
      note: refund
        ? refund.state === 'Succeeded'
          ? t('Refunded {amount} to your original payment method.', { amount })
          : t('Refund of {amount} is being processed.', { amount })
        : t('Refund information will appear here.'),
    }
  }
  if (ticket.boardingStatus === 'NoShow') {
    return {
      icon: 'person_off',
      tone: 'bg-surface-container text-on-surface-variant',
      badge: 'bg-surface-container text-on-surface-variant',
      note: t('You did not check in on time, so this ticket is not refundable.'),
    }
  }
  return {
    icon: 'check_circle',
    tone: 'bg-mist text-teal-flow',
    badge: 'bg-secondary-container text-on-secondary-container',
    note: '',
  }
}

/** One finished booking: the journey once, then one line per passenger with that ticket's outcome and refund note. */
export default function PastJourneyRow({
  tickets,
  onOpen,
}: {
  tickets: Ticket[]
  onOpen: (id: string) => void
}) {
  const ticket = tickets[0]
  const look = closedTicketLook(ticket)
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl bg-white p-space-md shadow-card">
      <div className="flex items-center gap-3">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${look.tone}`}>
          <Icon name={look.icon} className="text-2xl" />
        </span>
        <div>
          <div className="flex flex-wrap items-center gap-2 text-sm font-semibold text-deep-river">
            {ticket.departShort} → {ticket.arriveShort}
            <span className={`rounded px-2 py-0.5 text-2xs font-semibold ${look.badge}`}>
              {bookingStatusLabel(tickets)}
            </span>
          </div>
          <div className="text-xs text-on-surface-variant">
            {t('{dateLabel} • {departTime} Departure • {count} passengers', {
              dateLabel: ticket.dateLabel,
              departTime: ticket.departTime,
              count: tickets.length,
            })}
          </div>
          {tickets.map((each) => {
            const note = closedTicketLook(each).note
            return (
              <div key={each.id} className="mt-0.5 text-xs text-deep-river">
                <span className="font-semibold">{each.seat}</span> · {each.passenger}
                {note && <span className="font-medium"> — {note}</span>}
              </div>
            )
          })}
        </div>
      </div>
      <Link
        to={ROUTES.ticketDetail}
        onClick={() => onOpen(ticket.id)}
        className="shrink-0 whitespace-nowrap rounded-lg border border-outline-variant/50 bg-white px-3 py-1.5 text-xs font-semibold text-deep-river transition-colors hover:bg-surface-container-low"
      >
        {t('View Summary')}
      </Link>
    </div>
  )
}
