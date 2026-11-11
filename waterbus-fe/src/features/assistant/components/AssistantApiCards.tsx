import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { AssistantApiCard } from '../types'
import { cardDeparture, cardLetter, cardPrice } from '../utils'

/** Path inside this app for a backend booking link, or null for a missing or foreign link. */
function localPathOf(bookingUrl: string | null): string | null {
  if (!bookingUrl) return null
  try {
    const url = new URL(bookingUrl, window.location.origin)
    return url.origin === window.location.origin ? `${url.pathname}${url.search}` : null
  } catch {
    return null // Không hiển thị liên kết không hợp lệ.
  }
}

/**
 * The trips an answer found, as cards labelled A, B, C… Every value comes from the backend's trip
 * search; nothing here is made up from the AI's text.
 */
export default function AssistantApiCards({
  cards,
  onSelect,
  selectedId = null,
}: {
  cards: AssistantApiCard[]
  onSelect: (card: AssistantApiCard) => void
  /** The trip the traveler picked, highlighted until they continue or pick another. */
  selectedId?: string | null
}) {
  return (
    <ul className="mt-2 space-y-2" aria-label={t('Trips found')}>
      {cards.map((card, index) => {
        const letter = cardLetter(index)
        const { time, date } = cardDeparture(card.departureTime)
        const price = cardPrice(card)
        const soldOut = card.availableSeatsCount <= 0
        const selected = card.tripId === selectedId
        const canChoose = Boolean(card.boardingStationId && card.disembarkingStationId) && !soldOut
        const localPath = canChoose ? null : localPathOf(card.bookingUrl)

        return (
          <li
            key={card.tripId}
            className={`rounded-xl border bg-white p-3 transition-shadow ${
              selected ? 'border-teal-flow shadow-md ring-2 ring-teal-flow/20' : 'border-outline-variant/50'
            }`}
          >
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-center gap-2.5">
                <span
                  aria-hidden="true"
                  className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg text-xs font-bold ${
                    selected ? 'bg-teal-flow text-white' : 'bg-deep-river text-white'
                  }`}
                >
                  {letter}
                </span>
                <div>
                  <div className="font-display text-lg font-bold leading-none text-deep-river">{time}</div>
                  <div className="mt-1 text-xs text-on-surface-variant">{date}</div>
                </div>
              </div>
              <span
                className={`shrink-0 rounded-full px-2 py-0.5 text-2xs font-semibold ${
                  soldOut ? 'bg-surface-container text-on-surface-variant' : 'bg-teal-flow/10 text-teal-flow'
                }`}
              >
                {soldOut ? t('Sold out') : t('{count} Seats Left', { count: card.availableSeatsCount })}
              </span>
            </div>

            <p className="mt-2 flex flex-wrap items-center gap-1 text-sm font-semibold text-deep-river">
              <span>{card.departureStationName}</span>
              <Icon name="arrow_forward" className="text-base text-teal-flow" />
              <span>{card.arrivalStationName}</span>
            </p>
            <p className="text-xs text-on-surface-variant">{card.routeName}</p>

            <div className="mt-2 flex flex-wrap items-center justify-between gap-2 border-t border-surface-container pt-2">
              <span className={`text-xs ${price ? 'font-bold text-deep-river' : 'text-on-surface-variant'}`}>
                {price ?? t('Price shown when you choose a seat')}
              </span>
              {canChoose ? (
                <button
                  type="button"
                  aria-pressed={selected}
                  onClick={() => onSelect(card)}
                  className={`inline-flex min-h-[36px] items-center gap-1 rounded-full px-3.5 text-xs font-semibold transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow ${
                    selected
                      ? 'bg-teal-flow text-white'
                      : 'border border-teal-flow text-teal-flow hover:bg-teal-flow hover:text-white'
                  }`}
                >
                  {selected && <Icon name="check" className="text-base" />}
                  {t('Choose trip {letter}', { letter })}
                </button>
              ) : (
                localPath && (
                  <Link to={localPath} className="text-xs font-semibold text-teal-flow underline">
                    {t('View trip')}
                  </Link>
                )
              )}
            </div>
          </li>
        )
      })}
    </ul>
  )
}
