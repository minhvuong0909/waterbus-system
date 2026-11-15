import { Button } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import type { TicketLookupBooking, TicketLookupResult } from '../types'
import { bookingStatusLook, ticketStatusLabel } from '../utils'
import QrCode from './QrCode'
import StatusPill from './StatusPill'

function BookingBlock({ booking }: { booking: TicketLookupBooking }) {
  return (
    <section className="rounded-2xl border border-outline-variant/40 bg-white p-space-lg shadow-sm">
      <div className="flex items-start justify-between gap-space-sm">
        <div>
          <h2 className="font-display text-title-md text-deep-river">{booking.routeName}</h2>
          <p className="text-sm text-on-surface-variant">
            {booking.boardingStation} → {booking.disembarkingStation}
          </p>
          <p className="text-sm text-on-surface-variant">{formatDateTime(booking.departureTime)}</p>
        </div>
        <StatusPill look={bookingStatusLook(booking.bookingStatus)} />
      </div>
      <p className="mt-space-sm text-xs text-on-surface-variant">
        {t('Booking code')}: <span className="font-mono">{booking.bookingCode}</span>
      </p>

      {booking.tickets.length === 0 ? (
        <p className="mt-space-md text-sm text-on-surface-variant">{t('No tickets have been issued for this booking yet.')}</p>
      ) : (
        <ul className="mt-space-md divide-y divide-outline-variant/40">
          {booking.tickets.map((ticket) => (
            <li key={ticket.ticketCode} className="flex items-center justify-between gap-space-sm py-space-sm text-sm">
              <div>
                <p className="font-semibold text-deep-river">{ticket.passengerName}</p>
                <p className="text-on-surface-variant">
                  {t('Seat')} {ticket.seatCode} · {ticket.seatClass}
                </p>
                <p className="font-mono text-xs text-on-surface-variant">{ticket.ticketCode}</p>
              </div>
              <span className="text-xs font-semibold text-on-surface-variant">
                {ticketStatusLabel(ticket.ticketStatus, ticket.boardingStatus)}
              </span>
            </li>
          ))}
        </ul>
      )}

      {booking.qr && (
        <div className="mt-space-md flex flex-col items-center gap-space-xs rounded-xl bg-mist p-space-md">
          <QrCode value={booking.qr.payload} size={200} label={`${t('Boarding QR')} ${booking.bookingCode}`} />
          <p className="text-center text-xs text-on-surface-variant">
            {t('One QR for every passenger of this booking. Show it to the staff when boarding.')}
          </p>
        </div>
      )}
    </section>
  )
}

/** Tickets found by the lookup. Shown from memory only; leaving the page clears it. */
export default function TicketLookupResultView({ result, onDone }: { result: TicketLookupResult; onDone: () => void }) {
  return (
    <div className="flex w-full flex-col gap-space-md">
      {result.bookings.length === 0 ? (
        <p className="rounded-2xl border border-outline-variant/40 bg-white p-space-lg text-sm text-on-surface-variant">
          {t('No tickets found for this order.')}
        </p>
      ) : (
        result.bookings.map((booking) => <BookingBlock key={booking.bookingCode} booking={booking} />)
      )}
      <Button variant="text" onClick={onDone}>
        {t('Look up another booking')}
      </Button>
    </div>
  )
}
