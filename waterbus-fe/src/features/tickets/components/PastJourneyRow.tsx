import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
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

export default function PastJourneyRow({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  const look = closedTicketLook(ticket)
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl bg-white p-space-md shadow-[0_1px_8px_rgba(13,37,56,0.05)]">
      <div className="flex items-center gap-3">
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${look.tone}`}>
          <Icon name={look.icon} className="text-[22px]" />
        </span>
        <div>
          <div className="flex flex-wrap items-center gap-2 text-sm font-semibold text-deep-river">
            {ticket.departShort} → {ticket.arriveShort}
            <span className={`rounded px-2 py-0.5 text-[10px] font-semibold ${look.badge}`}>
              {ticket.status}
            </span>
          </div>
          <div className="text-xs text-on-surface-variant">
            {t('{dateLabel} • {departTime} Departure • {seat} (1 Passenger)', {
              dateLabel: ticket.dateLabel,
              departTime: ticket.departTime,
              seat: ticket.seat,
            })}
          </div>
          {look.note && <div className="mt-0.5 text-xs font-medium text-deep-river">{look.note}</div>}
        </div>
      </div>
      <Link
        to={ROUTES.ticketDetail}
        onClick={() => onOpen(ticket.id)}
        className="whitespace-nowrap rounded-lg border border-outline-variant/50 bg-white px-3 py-1.5 text-xs font-semibold text-deep-river transition-colors hover:bg-surface-container-low"
      >
        {t('View Summary')}
      </Link>
    </div>
  )
}
