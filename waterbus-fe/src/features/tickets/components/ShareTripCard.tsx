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
  bookingId: string
  accessToken: string | null
}

/**
 * Passenger TripAccess Link (BR-ID-05): người mua gửi cho hành khách đi cùng để họ xem hành trình.
 * Người nhận không thấy giá, thanh toán hay QR lên tàu (BR-ID-03, BR-NOT-02).
 */
export default function ShareTripCard({ orderId, bookingId, accessToken }: ShareTripCardProps) {
  const [link, setLink] = useState<AccessLink>()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  const create = async () => {
    setBusy(true)
    setError(undefined)
    try {
      setLink(await createTripShareLink(orderId, bookingId, accessToken))
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
