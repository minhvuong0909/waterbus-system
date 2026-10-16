import { useMemo } from 'react'
import QRCode from 'qrcode'
import { t } from '../../i18n'

interface QrCodeProps {
  /** Text encoded in the code: the signed booking credential from the backend (RS256 token). */
  value: string
  size?: number
  className?: string
  /** Accessible name; defaults to a generic boarding QR label (the value can be very long). */
  label?: string
}

const QUIET_ZONE = 2

/**
 * A scannable QR code of `value`. For a ticket, `value` is the credential returned by
 * `GET /checkouts/{orderId}/bookings/{bookingId}/qr`, which the Staff Scanner verifies offline.
 */
export default function QrCode({ value, size = 160, className = '', label }: QrCodeProps) {
  const { dark, count } = useMemo(() => {
    const qr = QRCode.create(value, { errorCorrectionLevel: 'M' })
    const modules = qr.modules
    const cells: string[] = []
    for (let y = 0; y < modules.size; y++) {
      for (let x = 0; x < modules.size; x++) {
        if (modules.get(x, y)) cells.push(`M${x + QUIET_ZONE} ${y + QUIET_ZONE}h1v1h-1z`)
      }
    }
    return { dark: cells.join(''), count: modules.size + QUIET_ZONE * 2 }
  }, [value])

  return (
    <svg
      viewBox={`0 0 ${count} ${count}`}
      width={size}
      height={size}
      role="img"
      aria-label={label ?? t('Boarding QR')}
      className={className}
      shapeRendering="crispEdges"
    >
      <rect width={count} height={count} fill="#fff" />
      <path d={dark} fill="#0D2538" />
    </svg>
  )
}
