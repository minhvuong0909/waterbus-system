import { Link } from 'react-router-dom'
import type { AssistantApiCard } from '../types'

export default function AssistantApiCards({
  cards,
  onSelect,
}: {
  cards: AssistantApiCard[]
  onSelect: (card: AssistantApiCard) => void
}) {
  return <div className="space-y-2">{cards.map((card, index) => {
    let localPath: string | null = null
    try {
      const url = card.bookingUrl ? new URL(card.bookingUrl, window.location.origin) : null
      if (url?.origin === window.location.origin) localPath = `${url.pathname}${url.search}`
    } catch { /* Không hiển thị liên kết không hợp lệ. */ }
    return <article key={card.tripId} className="rounded-xl border border-teal-flow/20 bg-white p-3 text-xs">
      <span className="mb-1 inline-block rounded bg-mist px-2 py-0.5 font-semibold text-teal-flow">{String.fromCharCode(65 + index)}</span>
      <strong>{card.routeName}</strong>
      <p>{card.departureStationName} → {card.arrivalStationName}</p>
      <p>{new Date(card.departureTime).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
      <p>{card.availableSeatsCount} ghế còn trống · {(card.isPriceVerified || card.isDevelopmentPreview) && card.basePrice !== null
        ? `${card.basePrice.toLocaleString('vi-VN')} ₫`
        : 'Giá chưa được xác nhận'}</p>
      {card.boardingStationId && card.disembarkingStationId
        ? <button type="button" className="min-h-8 font-semibold text-teal-flow underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow" onClick={() => onSelect(card)}>Chọn chuyến {String.fromCharCode(65 + index)}</button>
        : localPath && <Link className="font-semibold text-teal-flow underline" to={localPath}>Xem chuyến</Link>}
    </article>
  })}</div>
}
