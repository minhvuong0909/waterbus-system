import { useState } from 'react'
import { Button, Icon, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { formatLongDate } from '../../../utils/time'
import { createTripShareLink } from '../services/orderAccessService'
import type { AccessLink } from '../types'
import { sharedTripUrl } from '../utils'
import LinkField from './LinkField'

interface ShareTripCardProps {
  orderId: string
  accessToken: string | null
  /** The bookings that can be shared; `label` tells them apart when there is more than one. */
  bookings: { bookingId: string; label?: string }[]
}

/**
 * Passenger TripAccess Link (BR-ID-05): người mua gửi cho hành khách đi cùng để họ xem hành trình.
 * Người nhận không thấy giá, thanh toán hay QR lên tàu (BR-ID-03, BR-NOT-02). Link cấp theo từng
 * đặt chỗ, nên đơn khứ hồi chọn chiều cần chia sẻ.
 */
export default function ShareTripCard({ orderId, accessToken, bookings }: ShareTripCardProps) {
  const [bookingId, setBookingId] = useState(bookings[0].bookingId)
  const [links, setLinks] = useState<Record<string, AccessLink>>({})
  const link = links[bookingId]
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  const create = async () => {
    setBusy(true)
    setError(undefined)
    try {
      const created = await createTripShareLink(orderId, bookingId, accessToken)
      setLinks((current) => ({ ...current, [bookingId]: created }))
    } catch (cause) {
      setError(
        cause instanceof ApiError && cause.status === 409
          ? t('Only a paid booking can be shared.')
          : t('That did not work. Please try again.'),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="rounded-2xl bg-white p-space-lg shadow-card">
      <h2 className="flex items-center gap-2 font-display text-lg font-bold text-deep-river">
        <Icon name="send" className="text-2xl text-teal-flow" />
        {t('Share this journey')}
      </h2>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t('Send fellow passengers a link to follow the trip. It shows the journey and seats, never the price, payment or boarding QR.')}
      </p>

      {bookings.length > 1 && (
        <div className="mt-space-md inline-flex rounded-full bg-surface-container p-1">
          {bookings.map((booking) => (
            <button
              key={booking.bookingId}
              type="button"
              aria-pressed={booking.bookingId === bookingId}
              onClick={() => {
                setBookingId(booking.bookingId)
                setError(undefined)
              }}
              className={`rounded-full px-3.5 py-1.5 text-xs font-semibold transition-colors ${
                booking.bookingId === bookingId
                  ? 'bg-deep-river text-white'
                  : 'text-on-surface-variant hover:text-deep-river'
              }`}
            >
              {booking.label}
            </button>
          ))}
        </div>
      )}

      <div className="mt-space-md">
        {link ? (
          <>
            <LinkField url={sharedTripUrl(link.accessToken)} />
            <p className="mt-1 text-xs text-on-surface-variant">
              {t('Valid until {date}.', { date: formatLongDate(link.expiresAt) })}
            </p>
          </>
        ) : (
          <Button size="sm" variant="neutral" icon="add" onClick={create} disabled={busy}>
            {t('Create share link')}
          </Button>
        )}
      </div>

      {error && (
        <div className="mt-space-md">
          <Notice tone="error">{error}</Notice>
        </div>
      )}
    </section>
  )
}
