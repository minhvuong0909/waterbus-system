import { numberLocale, t } from '../../i18n'
import { formatVnd } from '../../utils/format'
import { formatClock, toVietnamDate } from '../../utils/time'
import type { AssistantApiCard } from './types'

/** Same limit as the backend validator (AiBookingChatRequestValidator). */
export const MAX_MESSAGE_LENGTH = 1000
/** Recent turns sent with each question, so a follow-up keeps its context without a server session. */
export const MAX_HISTORY_TURNS = 8

/** A, B, C… for the cards of one answer. */
export const cardLetter = (index: number): string => String.fromCharCode(65 + index)

/** Departure of a card in Vietnam time: `{ time: "15:00", date: "Tomorrow · Wed, 21/10" }`. */
export function cardDeparture(iso: string, now = new Date()): { time: string; date: string } {
  const departure = new Date(iso)
  const day = toVietnamDate(departure)
  const label = new Intl.DateTimeFormat(numberLocale(), {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
  }).format(departure)
  const relative =
    day === toVietnamDate(now)
      ? t('Today')
      : day === toVietnamDate(new Date(now.getTime() + 86_400_000))
        ? t('Tomorrow')
        : null
  return { time: formatClock(departure), date: relative ? `${relative} · ${label}` : label }
}

/**
 * The fare to show on a card, or null while it is not known: the backend has not verified it, or
 * the trip search sent 0 (the real fares belong to each seat class).
 */
export function cardPrice(card: Pick<AssistantApiCard, 'basePrice' | 'isPriceVerified' | 'isDevelopmentPreview'>): string | null {
  if (card.basePrice === null || card.basePrice < 0) return null
  if (card.basePrice === 0 && !card.isDevelopmentPreview) return null
  if (!card.isPriceVerified && !card.isDevelopmentPreview) return null
  return formatVnd(card.basePrice)
}

const CHOICE =
  /^(?:chọn|choose|pick|select)\s+(?:chuyến\s+|trip\s+)?(a|b|c|d|e|f|g|h|i|j|10|[1-9]|đầu tiên|thứ hai|thứ ba|này|first|second|third|this)\s*[.!]?$/i

/**
 * A typed choice such as "chọn chuyến A", "chọn 2" or "choose trip B": the index of the card, -1
 * when it cannot tell which one, or null when the text is not a choice at all (a normal question).
 */
export function parseCardChoice(text: string, cardCount: number): number | null {
  const match = text.trim().match(CHOICE)
  if (!match) return null
  const choice = match[1].toLowerCase()
  if (/^[a-j]$/.test(choice)) return choice.charCodeAt(0) - 97
  if (/^(10|[1-9])$/.test(choice)) return Number(choice) - 1
  if (choice === 'đầu tiên' || choice === 'first') return 0
  if (choice === 'thứ hai' || choice === 'second') return 1
  if (choice === 'thứ ba' || choice === 'third') return 2
  return cardCount === 1 ? 0 : -1
}
