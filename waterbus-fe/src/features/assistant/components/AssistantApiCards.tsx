import { Link } from 'react-router-dom'
import type { AssistantApiCard } from '../types'

export default function AssistantApiCards({ cards }: { cards: AssistantApiCard[] }) {
  return <div className="space-y-2">{cards.map((card) => {
    let localPath: string | null = null
    try {
      const url = card.bookingUrl ? new URL(card.bookingUrl, window.location.origin) : null
      if (url?.origin === window.location.origin) localPath = `${url.pathname}${url.search}`
    } catch { /* Không hiển thị liên kết không hợp lệ. */ }
    return <article key={card.tripId} className="rounded-xl border border-teal-flow/20 bg-white p-3 text-xs">
      <strong>{card.routeName}</strong>
      <p>{card.departureStationName} → {card.arrivalStationName}</p>
      <p>{new Date(card.departureTime).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      <p>{card.availableSeatsCount} ghế còn trống · {card.isPriceVerified && card.basePrice !== null
        ? `${card.basePrice.toLocaleString('vi-VN')} ₫` : 'Giá chưa được xác nhận'}</p>
      {localPath && <Link className="font-semibold text-teal-flow underline" to={localPath}>Xem chuyến</Link>}
    </article>
  })}</div>
}
