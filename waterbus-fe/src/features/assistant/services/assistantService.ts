import { withDelay } from '../../../mocks/delay'
import { ASSISTANT_FALLBACK, ASSISTANT_REPLIES } from '../../../mocks/tickets'
import { DEFAULT_SEARCH_QUERY } from '../../../mocks/tripSearch'
import { t } from '../../../i18n'
import { toIsoDate } from '../../../utils/time'
import { getPierOptions, getTrips } from '../../trips/services/tripService'
import { pickPiers } from '../../trips/utils'
import type { AssistantReply } from '../types'

const MAX_TRIP_CARDS = 3
const TRIP_QUESTION = /trip|departure|schedule|timetable|next|chuyến|giờ|lịch/i

/** Minutes since midnight of an "HH:mm" time. */
const minutesOf = (time: string) => {
  const [hours, minutes] = time.split(':').map(Number)
  return hours * 60 + minutes
}

/** The next departures of the main corridor today, from the same trip search the app uses. */
async function nextDepartures(): Promise<AssistantReply> {
  const piers = await getPierOptions()
  const search = { ...pickPiers(piers, DEFAULT_SEARCH_QUERY), dateId: toIsoDate(new Date()) }
  const trips = await getTrips({ ...DEFAULT_SEARCH_QUERY, ...search })

  const now = new Date()
  const upcoming = trips
    .filter((trip) => minutesOf(trip.departTime) >= now.getHours() * 60 + now.getMinutes())
    .slice(0, MAX_TRIP_CARDS)

  if (upcoming.length === 0) {
    return { text: t('There are no more departures today. Try another date on the timetable.') }
  }
  return { text: t('Here are the next departures today:'), trips: upcoming, search }
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
