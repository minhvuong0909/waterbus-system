import { t } from '../../../i18n'
import type { SharedTripPassenger } from '../types'
import { ticketStatusLabel } from '../utils'

/** Hành khách của một đặt chỗ được chia sẻ: tên, ghế và tình trạng vé/check-in của từng người. */
export default function SharedTripPassengers({ passengers }: { passengers: SharedTripPassenger[] }) {
  if (passengers.length === 0) {
    return (
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t('No ticket has been issued for this booking.')}
      </p>
    )
  }

  return (
    <ul className="mt-space-sm divide-y divide-surface-container">
      {passengers.map((passenger) => (
        <li
          key={passenger.seatCode}
          className="flex items-center justify-between gap-space-md py-space-sm"
        >
          <div className="flex min-w-0 items-center gap-3">
            <span className="flex h-9 min-w-[2.75rem] shrink-0 items-center justify-center rounded-lg bg-mist px-2 font-mono text-xs font-bold text-deep-river">
              {passenger.seatCode}
            </span>
            <span className="truncate font-semibold text-deep-river">{passenger.passengerName}</span>
          </div>
          <span className="shrink-0 text-sm text-on-surface-variant">
            {ticketStatusLabel(passenger.ticketStatus, passenger.boardingStatus)}
          </span>
        </li>
      ))}
    </ul>
  )
}
