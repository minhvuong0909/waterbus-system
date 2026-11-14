import { Link } from 'react-router-dom'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import { formatDateTime, formatLongDate } from '../../../utils/time'
import type { AccountOrder } from '../types'
import { accountOrderPath, bookingStatusLook, orderStatusLook } from '../utils'
import StatusPill from './StatusPill'

/**
 * Một đơn trong lịch sử của tài khoản. Trạng thái đơn và trạng thái từng đặt chỗ được hiện riêng,
 * không gộp. Chỉ đơn đã phát vé mới mở được trang quản lý đặt chỗ.
 */
export default function AccountOrderCard({ order }: { order: AccountOrder }) {
  const hasTickets = order.bookings.some((booking) => booking.ticketCount > 0)

  return (
    <article className="rounded-2xl bg-white p-space-lg shadow-card">
      <header className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-sm text-on-surface-variant">
          {t('Ordered {date}', { date: formatLongDate(order.createdAt) })}
          <span aria-hidden className="mx-2 text-outline">•</span>
          {order.purchaseMode === 'RoundTrip' ? t('Round trip') : t('One way')}
        </div>
        <StatusPill look={orderStatusLook(order.orderStatus)} />
      </header>

      <ul className="mt-space-md divide-y divide-surface-container border-y border-surface-container">
        {order.bookings.map((booking) => (
          <li
            key={booking.bookingId}
            className="flex flex-wrap items-center justify-between gap-x-space-md gap-y-1 py-space-sm"
          >
            <div className="min-w-0">
              <div className="truncate font-display text-base font-bold text-deep-river">
                {booking.routeName}
              </div>
              <div className="text-xs text-on-surface-variant">
                {formatDateTime(booking.departureTime)}
                <span aria-hidden className="mx-1.5 text-outline">•</span>
                <span className="font-mono">{booking.bookingCode}</span>
                {booking.ticketCount > 0 && (
                  <>
                    <span aria-hidden className="mx-1.5 text-outline">•</span>
                    {booking.ticketCount === 1
                      ? t('1 ticket')
                      : t('{count} tickets', { count: booking.ticketCount })}
                  </>
                )}
              </div>
            </div>
            <StatusPill look={bookingStatusLook(booking.bookingStatus)} />
          </li>
        ))}
      </ul>

      <footer className="mt-space-md flex items-center justify-between gap-space-md">
        <div className="text-sm text-on-surface-variant">
          {t('Total')}{' '}
          <span className="font-display text-base font-bold text-deep-river">
            {formatVnd(order.quotedTotal)}
          </span>
        </div>
        {hasTickets && (
          <Link
            to={accountOrderPath(order.orderId)}
            className="tap-area text-sm font-semibold text-teal-flow hover:underline"
          >
            {t('View booking')}
          </Link>
        )}
      </footer>
    </article>
  )
}
