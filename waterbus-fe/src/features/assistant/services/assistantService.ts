import { withDelay } from '../../../mocks/delay'
import { ASSISTANT_FALLBACK, ASSISTANT_REPLIES } from '../../../mocks/tickets'
import { t } from '../../../i18n'
import { getUpcomingTrips } from '../../trips/services/tripService'
import type { AssistantReply } from '../types'

const MAX_TRIP_CARDS = 3
const TRIP_QUESTION = /trip|departure|schedule|timetable|next|chuyến|giờ|lịch/i

/** The next departures of the main corridor today, from the same trip search the app uses. */
async function nextDepartures(): Promise<AssistantReply> {
  const { trips, search } = await getUpcomingTrips(MAX_TRIP_CARDS)
  if (trips.length === 0) {
    return { text: t('There are no more departures today. Try another date on the timetable.') }
  }
  return { text: t('Here are the next departures today:'), trips, search }
}

/**
 * Mock of the AI assistant (Semantic Kernel, roadmap Phase 4). Questions about trips are answered
 * with real trip cards from the trip search; chip questions get canned replies; anything else gets
 * a polite fallback. Replace with a call to the chat endpoint once the backend has one.
 *
 * @param tripCards set to false in a chat that cannot render trip cards
 */
export async function askAssistant(
  message: string,
  { tripCards = true }: { tripCards?: boolean } = {},
): Promise<AssistantReply> {
  const canned = ASSISTANT_REPLIES[message]
  if (canned) return withDelay({ text: canned }, 500)
  if (tripCards && TRIP_QUESTION.test(message)) return nextDepartures()
  return withDelay({ text: ASSISTANT_FALLBACK }, 500)
}
