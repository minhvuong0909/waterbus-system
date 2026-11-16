import { Icon } from '../../../components/ui'
import type { Ticket } from '../types'

/**
 * Danh sách hành khách của một Booking: ghế, tên và trạng thái từng vé. Booking chỉ có một QR chung (BR-QR-01),
 * nên từng vé không có mã QR riêng; Staff chọn đúng những người có mặt khi quét (BR-QR-03).
 */
export default function PassengerList({ tickets }: { tickets: Ticket[] }) {
  return (
    <ul className="divide-y divide-surface-container overflow-hidden rounded-xl bg-mist/60">
      {tickets.map((ticket) => (
        <li key={ticket.id} className="flex items-center justify-between gap-3 px-space-sm py-2 text-sm">
          <span className="flex min-w-0 items-center gap-2">
            <Icon name="chair" className="shrink-0 text-base text-teal-flow" />
            <span className="font-semibold text-deep-river">{ticket.seat}</span>
            <span className="truncate text-on-surface-variant">{ticket.passenger}</span>
          </span>
          <span className="shrink-0 rounded-full bg-secondary-container px-2.5 py-0.5 text-2xs font-semibold text-on-secondary-container">
            {ticket.status}
          </span>
        </li>
      ))}
    </ul>
  )
}
