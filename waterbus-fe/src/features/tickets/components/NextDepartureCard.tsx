import { Link } from 'react-router-dom'
import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import { isTicketToday } from '../utils'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

function Fact({ label, value, icon }: { label: string; value: string; icon?: string }) {
  return (
    <div>
      <div className="text-2xs text-on-surface-variant">{label}</div>
      <div className="flex items-center gap-1 text-sm font-semibold text-deep-river">
        {icon && <Icon name={icon} className="text-base text-teal-flow" />}
        {value}
      </div>
    </div>
  )
}

/** Featured "Next Departure" card: journey on the left, corridor + QR on the right. */
export default function NextDepartureCard({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  return (
    <div className="relative overflow-hidden rounded-2xl bg-surface-container-lowest shadow-raised">
      <div className="h-1.5 bg-gradient-to-r from-teal-flow via-sky-aqua to-deep-river" />
      <div className="grid grid-cols-1 lg:grid-cols-12">
        <div className="flex flex-col p-space-lg lg:col-span-8">
          <div className="flex flex-wrap items-start justify-between gap-space-sm">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-mist text-teal-flow">
                <Icon name="directions_boat" className="text-2xl" />
              </span>
              <div>
                <div className="text-sm font-semibold text-deep-river">{ticket.lineName}</div>
                <div className="font-mono text-2xs text-on-surface-variant">
                  {t('Ref: {bookingRef}', { bookingRef: ticket.bookingRef })}
                </div>
              </div>
            </div>
            <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
              <Icon name="verified" className="text-sm" />
              {t('Confirmed Booking')}
            </span>
          </div>

          <div className="mt-space-md grid grid-cols-1 items-center gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-12">
            <div className="md:col-span-4">
              <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
                {t('Origin Pier')}
              </div>
              <div className="font-display text-3xl font-bold text-deep-river">
                {ticket.departTime}
              </div>
              <div className="font-display text-base font-semibold text-deep-river">
                {ticket.departPier}
              </div>
              <div className="text-xs text-on-surface-variant">{ticket.departDistrict}</div>
            </div>
            <div className="flex flex-col items-center md:col-span-4">
              <span className="text-xs text-teal-flow">
                {t('{durationMins} min crossing', { durationMins: ticket.durationMins })}
              </span>
              <div className="my-1 flex w-full items-center gap-1">
                <span className="h-2 w-2 rounded-full bg-deep-river" />
                <span className="h-px flex-1 bg-outline-variant" />
                <Icon name="waves" className="text-base text-teal-flow" />
                <span className="h-px flex-1 bg-outline-variant" />
                <span className="h-2 w-2 rounded-full bg-teal-flow" />
              </div>
            </div>
            <div className="md:col-span-4 md:text-right">
              <div className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
                {t('Destination Pier')}
              </div>
              <div className="font-display text-3xl font-bold text-deep-river">
                {ticket.arriveTime}
              </div>
              <div className="font-display text-base font-semibold text-deep-river">
                {ticket.arrivePier}
              </div>
              <div className="text-xs text-on-surface-variant">{ticket.arriveDistrict}</div>
            </div>
          </div>

          <div className="mt-space-md grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md md:grid-cols-4">
            <Fact
              label={t('Travel Date')}
              value={isTicketToday(ticket) ? t('Today, {label}', { label: ticket.dateShort }) : ticket.dateShort}
            />
            <Fact label={t('Passenger')} value={ticket.passenger} />
            <Fact
              label={t('Assigned Seat')}
              value={ticket.windowSeat ? t('{seat} (Window)', { seat: ticket.seat }) : ticket.seat}
              icon="chair"
            />
            <Fact label={t('Fare Paid')} value={formatVnd(ticket.fareVnd)} />
          </div>

          <div className="mt-auto flex items-center justify-between border-t border-surface-container pt-space-md text-xs">
            <Link
              to={ROUTES.manageBooking}
              className="flex items-center gap-1 font-semibold text-teal-flow hover:underline"
            >
              <Icon name="info" className="text-sm" />
              {t('View Booking Details')}
            </Link>
            <span className="text-on-surface-variant">
              {t('Vessel: {vesselNote}', { vesselNote: ticket.vesselNote })}
            </span>
          </div>
        </div>

        <div className="space-y-space-md bg-mist/60 p-space-lg lg:col-span-4">
          <div className="rounded-xl bg-white p-space-md shadow-sm">
            {/* Title and piers on separate lines: real pier names are too long to share one row. */}
            <div className="flex flex-col gap-0.5 text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
              {t('Corridor Tracking')}
              <span className="flex items-center gap-1 normal-case tracking-normal text-teal-flow">
                <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" /> {`${ticket.departShort} → ${ticket.arriveShort}`}
              </span>
            </div>
            <svg viewBox="0 0 240 60" className="mt-2 h-14 w-full" aria-hidden="true">
              <rect width="240" height="60" rx="8" fill="#EEF5F4" />
              <path
                d="M10 34 C 60 18 90 48 130 32 C 170 16 200 40 230 30"
                fill="none"
                stroke="#4FC3D8"
                strokeWidth="3"
                strokeDasharray="4 4"
              />
              <circle cx="24" cy="30" r="5" fill="#0D2538" />
              <circle cx="216" cy="32" r="5" fill="#147A7E" />
              <circle cx="120" cy="30" r="8" fill="#147A7E" stroke="#fff" strokeWidth="2" />
            </svg>
            <div className="mt-1 flex justify-between text-2xs text-on-surface-variant">
              <span>{ticket.departShort}</span>
              <span>{ticket.arriveShort}</span>
            </div>
          </div>

          <div className="text-center">
            <div className="mx-auto w-fit rounded-xl bg-white p-2 shadow-sm">
              {ticket.qrPayload && <QrCode value={ticket.qrPayload} size={120} />}
            </div>
            <div className="mt-2 font-mono text-2xs font-semibold text-deep-river">
              {ticket.ticketCode}
            </div>
            <div className="flex items-center justify-center gap-1 text-2xs text-on-surface-variant">
              <Icon name="sensors" className="text-xs" /> {t('Scan at turnstile barrier')}
            </div>
          </div>

          <Link
            to={ROUTES.ticketDetail}
            onClick={() => onOpen(ticket.id)}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
          >
            <Icon name="qr_code_2" className="text-lg" />
            {t('View Full Ticket')}
          </Link>
          <Link
            to={`${ROUTES.liveTracking}?tripId=${ticket.tripId}`}
            className="flex w-full items-center justify-center gap-2 rounded-xl border border-outline-variant/50 bg-white py-3 text-sm font-semibold text-deep-river transition-colors hover:bg-surface-container-low"
          >
            <Icon name="near_me" className="text-lg" />
            {t('Track Live Vessel')}
          </Link>
        </div>
      </div>
    </div>
  )
}
