import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd, formatVndSuffix } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

interface BoardingPassProps {
  ticket: Ticket
  onEnlarge: () => void
}

/** Main boarding pass on the ticket detail page: journey, facts, large QR and actions. */
export function TicketBoardingPass({ ticket, onEnlarge }: BoardingPassProps) {
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
            <div className="text-[11px] text-on-surface-variant">{t('1 Adult Ticket')}</div>
          </div>
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Travel Date')}</dt>
            <dd className="text-sm font-semibold text-deep-river">{ticket.dateLabel}</dd>
            <div className="text-[11px] text-teal-flow">
              {ticket.completed ? t('Completed') : t('Today')}
            </div>
          </div>
          <div>
            <dt className="text-[11px] text-on-surface-variant">{t('Total Paid')}</dt>
            <dd className="text-sm font-semibold text-deep-river">{formatVnd(ticket.fareVnd)}</dd>
            <div className="text-[11px] text-on-surface-variant">{t('Fare Tier: Standard')}</div>
          </div>
        </dl>

        <div className="mx-auto mt-space-lg w-fit rounded-2xl bg-white p-space-md text-center shadow-sm">
          <QrCode value={ticket.ticketCode} size={200} className="mx-auto" />
          <div className="mt-2 font-mono text-lg font-bold tracking-wider text-deep-river">
            {ticket.ticketCode}
          </div>
          <div className="flex items-center justify-center gap-1 text-xs font-medium text-teal-flow">
            <Icon name="qr_code_scanner" className="text-[15px]" />
            {t('Hold near scanner or present to boarding staff')}
          </div>
        </div>

        <div className="mt-space-lg grid grid-cols-1 gap-space-sm sm:grid-cols-2">
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

export function TicketCorridorCard({ ticket }: { ticket: Ticket }) {
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
          <Icon name="explore" className="text-[20px] text-teal-flow" />
          {t('River Crossing Corridor')}
        </h2>
        <span className="rounded bg-secondary-container px-2 py-0.5 text-[10px] font-bold text-teal-flow">
          {t('{tripCode} Route', { tripCode: ticket.tripCode })}
        </span>
      </div>
      <div className="relative h-48 overflow-hidden rounded-xl bg-[#E6EFEC]">
        <svg
          className="absolute inset-0 h-full w-full"
          viewBox="0 0 400 190"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path
            d="M0 40 C 120 50 200 100 300 130 C 340 142 370 160 400 190 L 0 190 Z"
            fill="#4FC3D8"
            opacity="0.35"
          />
          <path
            d="M60 50 C 160 70 240 108 340 150"
            fill="none"
            stroke="#147A7E"
            strokeWidth="3"
            strokeDasharray="6 6"
          />
        </svg>
        <span className="absolute left-3 top-3 rounded-lg bg-white/95 px-2.5 py-1 text-[11px] shadow-sm">
          <b className="text-deep-river">{ticket.departPier}</b>
          <span className="block text-on-surface-variant">
            {t('{departDistrict} • Departure', { departDistrict: ticket.departDistrict })}
          </span>
        </span>
        <span className="absolute left-1/2 top-1/2 flex -translate-x-1/2 -translate-y-1/2 items-center gap-1.5 rounded-full bg-deep-river px-3 py-1.5 text-[11px] font-bold text-white shadow-lg">
          <Icon name="directions_boat" className="text-[14px]" />
          {t('Waterbus {tripCode}', { tripCode: ticket.tripCode })}
        </span>
        <span className="absolute bottom-3 right-3 rounded-lg bg-white/95 px-2.5 py-1 text-[11px] shadow-sm">
          <b className="text-deep-river">{ticket.arrivePier}</b>
          <span className="block text-on-surface-variant">
            {t('{arriveDistrict} • Arrival', { arriveDistrict: ticket.arriveDistrict })}
          </span>
        </span>
      </div>
      <div className="mt-space-md grid grid-cols-3 gap-2 text-center">
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Scheduled Crossing')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {t('{durationMins} Minutes', { durationMins: ticket.durationMins })}
          </div>
        </div>
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Corridor Line')}</div>
          <div className="text-sm font-semibold text-deep-river">{t('Direct Line')}</div>
        </div>
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Passenger')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {t('1 Pass ({seat})', { seat: ticket.seat })}
          </div>
        </div>
      </div>
    </div>
  )
}

export function TicketActions({ onShowQr }: { onShowQr: () => void }) {
  return (
    <div className="space-y-space-sm rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <button
        type="button"
        onClick={onShowQr}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
      >
        <Icon name="qr_code" className="text-[18px]" />
        {t('Show QR Ticket')}
      </button>
      <Link
        to={ROUTES.liveTracking}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-surface-container py-3 text-sm font-semibold text-deep-river transition-colors hover:bg-surface-container-high"
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

export function BoardingGuidance({ ticket }: { ticket: Ticket }) {
  return (
    <div className="flex flex-col items-start justify-between gap-space-md rounded-2xl bg-sand-light/70 p-space-md sm:flex-row sm:items-center">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-secondary-container text-teal-flow">
          <Icon name="confirmation_number" className="text-[22px]" />
        </span>
        <div>
          <div className="font-headline-sm text-base font-bold text-deep-river">
            {t('Boarding Guidance')}
          </div>
          <ul className="flex flex-col gap-x-6 text-xs text-on-surface-variant sm:flex-row">
            <li className="flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {t('Keep your QR ticket ready on your screen before boarding.')}
            </li>
            <li className="flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {t('Present the QR ticket to staff when boarding at {departPier}.', {
                departPier: ticket.departPier,
              })}
            </li>
          </ul>
        </div>
      </div>
      <span className="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-2 text-xs text-deep-river shadow-sm">
        <Icon name="help_outline" className="text-[15px] text-teal-flow" />
        {t('Terminal Pier Gate 4')}
      </span>
    </div>
  )
}

/** Full-screen QR view for fast turnstile scans. Closes on Escape or backdrop click. */
export function QrModal({ ticket, onClose }: { ticket: Ticket; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t('Boarding QR Code')}
      className="fixed inset-0 z-[60] flex items-center justify-center bg-deep-river/70 p-space-md backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-sm rounded-3xl bg-white p-space-xl text-center shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label={t('Close')}
          className="absolute right-4 top-4 flex h-9 w-9 items-center justify-center rounded-full bg-surface-container text-deep-river hover:bg-surface-container-high"
        >
          <Icon name="close" className="text-[20px]" />
        </button>
        <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Fast Turnstile Scan')}
        </span>
        <h2 className="mt-1 font-headline-sm text-2xl font-bold text-deep-river">
          {t('Boarding QR Code')}
        </h2>
        <p className="text-xs text-on-surface-variant">
          {ticket.departPier} → {ticket.arrivePier} ({ticket.seat})
        </p>
        <div className="mx-auto mt-space-md w-fit rounded-2xl border border-outline-variant/40 p-3">
          <QrCode value={ticket.ticketCode} size={260} />
        </div>
        <div className="mt-space-md font-mono text-lg font-bold tracking-wider text-deep-river">
          {ticket.ticketCode}
        </div>
        <div className="text-xs text-on-surface-variant">
          {t('Single Crossing Pass • {price}', { price: formatVndSuffix(ticket.fareVnd) })}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="mt-space-lg w-full rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('Done')}
        </button>
      </div>
    </div>
  )
}
