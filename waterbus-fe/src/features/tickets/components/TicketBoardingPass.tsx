import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { formatVnd } from '../../../utils/format'
import { isTicketToday } from '../utils'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

interface BoardingPassProps {
  ticket: Ticket
  onEnlarge: () => void
}

/** Main boarding pass on the ticket detail page: journey, facts, large QR and actions. */
export default function TicketBoardingPass({ ticket, onEnlarge }: BoardingPassProps) {
  return (
    <div className="overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_4px_24px_rgba(13,37,56,0.08)]">
      <div className="flex items-center justify-between bg-deep-river px-space-lg py-space-md text-on-primary">
        <div className="flex items-center gap-3">
          <Icon name="directions_boat" className="text-[24px]" />
          <div>
            <div className="font-headline-sm text-sm font-bold">
              {t('Smart Waterbus • Boarding Pass')}
            </div>
            <div className="text-[11px] text-sand-light/80">
              {t('Single Crossing • Central Route')}
            </div>
          </div>
        </div>
        <span className="inline-flex items-center gap-1 rounded-full bg-teal-flow/30 px-3 py-1 text-xs font-semibold text-sky-aqua">
          <Icon name="verified" className="text-[14px]" />
          {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
        </span>
      </div>

      <div className="relative p-space-lg">
        <div className="grid grid-cols-3 items-center gap-2">
          <div>
            <div className="text-[11px] text-on-surface-variant">{t('Departure')}</div>
            <div className="font-headline-sm text-4xl font-bold text-deep-river">
              {ticket.departTime}
            </div>
            <div className="font-headline-sm text-lg font-semibold text-deep-river">
              {ticket.departPier}
            </div>
            <div className="text-xs text-on-surface-variant">{ticket.departDistrict}</div>
          </div>
          <div className="flex flex-col items-center">
            <span className="rounded-full bg-secondary-container/60 px-2.5 py-0.5 text-[11px] text-teal-flow">
              {t('{durationMins} min river crossing', { durationMins: ticket.durationMins })}
            </span>
            <div className="my-2 flex w-full items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-deep-river" />
              <span className="h-px flex-1 bg-outline-variant" />
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              <span className="h-px flex-1 bg-outline-variant" />
              <span className="h-2 w-2 rounded-full bg-deep-river" />
            </div>
            <span className="flex items-center gap-1 text-[11px] text-on-surface-variant">
              {t('Direct Line')} <Icon name="swap_horiz" className="text-[13px] text-teal-flow" />
            </span>
          </div>
          <div className="text-right">
            <div className="text-[11px] text-on-surface-variant">{t('Arrival')}</div>
            <div className="font-headline-sm text-4xl font-bold text-deep-river">
              {ticket.arriveTime}
            </div>
            <div className="font-headline-sm text-lg font-semibold text-deep-river">
              {ticket.arrivePier}
            </div>
            <div className="text-xs text-on-surface-variant">{ticket.arriveDistrict}</div>
          </div>
        </div>

        <div className="relative my-space-lg border-t border-dashed border-outline-variant">
          <span className="absolute -left-[38px] -top-3 h-6 w-6 rounded-full bg-surface" />
          <span className="absolute -right-[38px] -top-3 h-6 w-6 rounded-full bg-surface" />
        </div>

        <dl className="grid grid-cols-2 gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-4">
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Assigned Seat')}</dt>
            <dd className="font-headline-sm text-xl font-bold text-teal-flow">{ticket.seat}</dd>
            <div className="text-[11px] text-on-surface-variant">{ticket.seatNote}</div>
          </div>
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Passenger')}</dt>
            <dd className="text-sm font-semibold text-deep-river">{ticket.passenger}</dd>
          </div>
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Travel Date')}</dt>
            <dd className="text-sm font-semibold text-deep-river">{ticket.dateLabel}</dd>
            <div className="text-[11px] text-teal-flow">
              {ticket.completed ? t('Completed') : isTicketToday(ticket) ? t('Today') : ticket.dateShort}
            </div>
          </div>
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Total Paid')}</dt>
            <dd className="text-sm font-semibold text-deep-river">{formatVnd(ticket.fareVnd)}</dd>
          </div>
        </dl>

        <div className="mx-auto mt-space-lg w-fit rounded-2xl bg-white p-space-md text-center shadow-sm">
          {ticket.qrPayload && <QrCode value={ticket.qrPayload} size={200} className="mx-auto" />}
          <div className="mt-2 font-mono text-lg font-bold tracking-wider text-deep-river">
            {ticket.ticketCode}
          </div>
          <div className="flex items-center justify-center gap-1 text-xs font-medium text-teal-flow">
            <Icon name="qr_code_scanner" className="text-[15px]" />
            {t('Hold near scanner or present to boarding staff')}
          </div>
        </div>

        <div className="mt-space-lg grid grid-cols-1 gap-space-sm sm:grid-cols-2 print:hidden">
          <button
            type="button"
            onClick={onEnlarge}
            className="flex items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
          >
            <Icon name="fullscreen" className="text-[18px]" />
            {t('Enlarge QR Ticket')}
          </button>
          <button
            type="button"
            onClick={() => window.print()}
            className="flex items-center justify-center gap-2 rounded-xl bg-surface-container py-3 text-sm font-semibold text-deep-river transition-colors hover:bg-surface-container-high"
          >
            <Icon name="print" className="text-[18px]" />
            {t('Print / Save PDF')}
          </button>
        </div>
      </div>
    </div>
  )
}
