import { Link } from 'react-router-dom'
import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

function Fact({ label, value, icon }: { label: string; value: string; icon?: string }) {
  return (
    <div>
      <div className="text-[11px] text-on-surface-variant">{label}</div>
      <div className="flex items-center gap-1 text-sm font-semibold text-deep-river">
        {icon && <Icon name={icon} className="text-[16px] text-teal-flow" />}
        {value}
      </div>
    </div>
  )
}

/** Featured "Next Departure" card: journey on the left, corridor + QR on the right. */
export function NextDepartureCard({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  return (
    <div className="relative overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_4px_24px_rgba(13,37,56,0.08)]">
      <div className="h-1.5 bg-gradient-to-r from-teal-flow via-sky-aqua to-deep-river" />
      <div className="grid grid-cols-1 lg:grid-cols-12">
        <div className="flex flex-col p-space-lg lg:col-span-8">
          <div className="flex flex-wrap items-start justify-between gap-space-sm">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-mist text-teal-flow">
                <Icon name="directions_boat" className="text-[22px]" />
              </span>
              <div>
                <div className="text-sm font-semibold text-deep-river">{ticket.lineName}</div>
                <div className="font-mono text-[11px] text-on-surface-variant">
                  {t('Ref: {bookingRef}', { bookingRef: ticket.bookingRef })}
                </div>
              </div>
            </div>
            <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
              <Icon name="verified" className="text-[14px]" />
              {t('Confirmed Booking')}
            </span>
          </div>

          <div className="mt-space-md grid grid-cols-1 items-center gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-12">
            <div className="md:col-span-4">
              <div className="text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                {t('Origin Pier')}
              </div>
              <div className="font-headline-sm text-3xl font-bold text-deep-river">
                {ticket.departTime}
              </div>
              <div className="font-headline-sm text-base font-semibold text-deep-river">
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
                <Icon name="waves" className="text-[16px] text-teal-flow" />
                <span className="h-px flex-1 bg-outline-variant" />
                <span className="h-2 w-2 rounded-full bg-teal-flow" />
              </div>
              <span className="text-[11px] text-on-surface-variant">{t('Direct Waterway')}</span>
            </div>
            <div className="md:col-span-4 md:text-right">
              <div className="text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                {t('Destination Pier')}
              </div>
              <div className="font-headline-sm text-3xl font-bold text-deep-river">
                {ticket.arriveTime}
              </div>
              <div className="font-headline-sm text-base font-semibold text-deep-river">
                {ticket.arrivePier}
              </div>
              <div className="text-xs text-on-surface-variant">{ticket.arriveDistrict}</div>
            </div>
          </div>

          <div className="mt-space-md grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md md:grid-cols-4">
            <Fact
              label={t('Travel Date')}
              value={t('Today, {label}', { label: ticket.dateShort })}
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
              <Icon name="info" className="text-[15px]" />
              {t('View Booking Details')}
            </Link>
            <span className="text-on-surface-variant">
              {t('Vessel: {vesselNote}', { vesselNote: ticket.vesselNote })}
            </span>
          </div>
        </div>

        <div className="space-y-space-md bg-mist/60 p-space-lg lg:col-span-4">
          <div className="rounded-xl bg-white p-space-md shadow-sm">
            <div className="flex items-center justify-between text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
              {t('Corridor Tracking')}
              <span className="flex items-center gap-1 normal-case text-teal-flow">
                <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" /> {t('Pier 01 → Pier 02')}
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
            <div className="mt-1 flex justify-between text-[10px] text-on-surface-variant">
              <span>{t('Bach Dang')}</span>
              <span>{t('Thu Thiem')}</span>
            </div>
          </div>

          <div className="text-center">
            <div className="mx-auto w-fit rounded-xl bg-white p-2 shadow-sm">
              <QrCode value={ticket.ticketCode} size={120} />
            </div>
            <div className="mt-2 font-mono text-[11px] font-semibold text-deep-river">
              {ticket.ticketCode}
            </div>
            <div className="flex items-center justify-center gap-1 text-[11px] text-on-surface-variant">
              <Icon name="sensors" className="text-[13px]" /> {t('Scan at turnstile barrier')}
            </div>
          </div>

          <Link
            to={ROUTES.ticketDetail}
            onClick={() => onOpen(ticket.id)}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
          >
            <Icon name="qr_code_2" className="text-[18px]" />
            {t('View Full Ticket')}
          </Link>
          <Link
            to={ROUTES.liveTracking}
            className="flex w-full items-center justify-center gap-2 rounded-xl border border-outline-variant/50 bg-white py-3 text-sm font-semibold text-deep-river transition-colors hover:bg-surface-container-low"
          >
            <Icon name="near_me" className="text-[18px]" />
            {t('Track Live Vessel')}
          </Link>
        </div>
      </div>
    </div>
  )
}

export function UpcomingTripCard({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs">
          <span className="rounded bg-mist px-2 py-1 font-semibold text-on-surface-variant">
            {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
          </span>
          <span className="text-on-surface-variant">• {ticket.routeTag}</span>
        </div>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-semibold text-on-secondary-container">
          {ticket.status}
        </span>
      </div>
      <div className="mt-space-md grid grid-cols-3 items-center">
        <div>
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
            {ticket.departTime}
          </div>
          <div className="text-sm font-semibold text-deep-river">{ticket.departPier}</div>
          <div className="text-xs text-on-surface-variant">{ticket.departDistrict}</div>
        </div>
        <div className="flex flex-col items-center text-xs text-teal-flow">
          {t('{durationMins} min', { durationMins: ticket.durationMins })}
          <Icon name="arrow_forward" className="text-[20px]" />
        </div>
        <div className="text-right">
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
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
          <div className="text-on-surface-variant">{t('Seat / Pax')}</div>
          <div className="font-semibold text-deep-river">
            {t('{seat} • 1 Adt', { seat: ticket.seat })}
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
          <Icon name="qr_code" className="text-[16px]" />
          {t('View Ticket')}
        </Link>
      </div>
    </div>
  )
}

export function PastJourneyRow({
  ticket,
  onOpen,
}: {
  ticket: Ticket
  onOpen: (id: string) => void
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-2xl bg-white p-space-md shadow-[0_1px_8px_rgba(13,37,56,0.05)]">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-mist text-teal-flow">
          <Icon name="check_circle" className="text-[22px]" />
        </span>
        <div>
          <div className="flex flex-wrap items-center gap-2 text-sm font-semibold text-deep-river">
            {ticket.departShort} → {ticket.arriveShort}
            <span className="rounded bg-secondary-container px-2 py-0.5 text-[10px] font-semibold text-on-secondary-container">
              {t('Completed')}
            </span>
          </div>
          <div className="text-xs text-on-surface-variant">
            {t('{dateLabel} • {departTime} Departure • {seat} (1 Passenger)', {
              dateLabel: ticket.dateLabel,
              departTime: ticket.departTime,
              seat: ticket.seat,
            })}
          </div>
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
