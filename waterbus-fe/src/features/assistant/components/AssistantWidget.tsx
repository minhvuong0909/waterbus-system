import { useEffect, useRef, useState } from 'react'
import type { FormEvent, KeyboardEvent as ReactKeyboardEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip, setQuery } from '../../booking/bookingSlice'
import { askAssistant } from '../services/assistantService'
import type { AssistantApiCard, AssistantChatTurn, AssistantSearchContext, AssistantScopeClarification, ChatMessage } from '../types'
import { MAX_HISTORY_TURNS, MAX_MESSAGE_LENGTH, cardDeparture, cardLetter, parseCardChoice } from '../utils'
import AssistantApiCards from './AssistantApiCards'
import { MessageBubble, Suggestions, TypingIndicator } from './AssistantParts'
import { t } from '../../../i18n'
import { toVietnamDate } from '../../../utils/time'

const welcome = (): ChatMessage => ({
  id: 0,
  from: 'assistant',
  text: t('Hi! I can show upcoming trips, or find trips by pier and date. What would you like to know?'),
})

/**
 * Chat bubble in the corner of every passenger page. Questions go to the AI API with the recent
 * turns; the assistant answers with text and trip cards taken from the real trip search. Picking a
 * card and continuing opens the normal booking flow: nothing is booked or paid from the chat.
 */
export default function AssistantWidget() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [draft, setDraft] = useState('')
  const [searchContext, setSearchContext] = useState<AssistantSearchContext | null>(null)
  const [pendingClarification, setPendingClarification] = useState<AssistantScopeClarification | null>(null)
  const [selected, setSelected] = useState<{ card: AssistantApiCard; letter: string } | null>(null)
  const [messages, setMessages] = useState<ChatMessage[]>(() => [welcome()])
  const nextId = useRef(1)
  const inputRef = useRef<HTMLTextAreaElement>(null)
  const launcherRef = useRef<HTMLButtonElement>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const wasOpen = useRef(false)

  // Focus the input when the panel opens, and give focus back to the launcher when it closes.
  useEffect(() => {
    if (open) inputRef.current?.focus()
    else if (wasOpen.current) launcherRef.current?.focus()
    wasOpen.current = open
  }, [open])

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [messages, busy, selected])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  const add = (message: Omit<ChatMessage, 'id'>) =>
    setMessages((list) => [...list, { ...message, id: nextId.current++ }])

  /** The turns sent as context: the real exchange only, not the welcome or local error notes. */
  const historyOf = (list: ChatMessage[]): AssistantChatTurn[] =>
    list
      .filter((m) => m.id !== 0 && m.tone !== 'error')
      .slice(-MAX_HISTORY_TURNS)
      .map((m) => ({ role: m.from, content: m.text }))

  const ask = async (question: string, history: AssistantChatTurn[]) => {
    setBusy(true)
    try {
      const answer = await askAssistant(question, history, searchContext, pendingClarification)
      if (answer.searchContext !== undefined) setSearchContext(answer.searchContext)
      setPendingClarification(answer.pendingClarification ?? null)
      setSelected(null)
      add({
        from: 'assistant',
        text: answer.text,
        apiCards: answer.apiCards,
        tone: answer.isFallback ? 'fallback' : undefined,
      })
    } catch {
      add({
        from: 'assistant',
        text: t('Sorry, I couldn’t reach the assistant. Please try again in a moment.'),
        tone: 'error',
        retryQuestion: question,
      })
    } finally {
      setBusy(false)
    }
  }

  const selectCard = (card: AssistantApiCard, letter: string) => {
    if (!card.boardingStationId || !card.disembarkingStationId) return
    setSelected({ card, letter })
    const { time, date } = cardDeparture(card.departureTime)
    const passengers = searchContext?.passengerCount
    add({
      from: 'assistant',
      text: [
        t('You picked trip {letter}: {from} → {to}, {time} {date}.', {
          letter,
          from: card.departureStationName,
          to: card.arrivalStationName,
          time,
          date,
        }),
        passengers ? t('For {count} passengers.', { count: passengers }) : '',
        t('This is not a booking yet. Continue to choose your seats and pay.'),
      ]
        .filter(Boolean)
        .join(' '),
    })
  }

  /** Shows the traveler's text, then either applies a typed card choice or asks the backend. */
  const send = (text: string, question = text) => {
    const clean = text.trim()
    if (!clean || busy) return
    const history = historyOf(messages)
    add({ from: 'user', text: clean })
    setDraft('')

    const latestCards = [...messages].reverse().find((m) => m.from === 'assistant' && m.apiCards?.length)?.apiCards ?? []
    const choice = parseCardChoice(clean, latestCards.length)
    if (choice !== null) {
      const card = latestCards[choice]
      if (card?.boardingStationId && card.disembarkingStationId) selectCard(card, cardLetter(choice))
      else add({ from: 'assistant', text: t('Pick one of the trips shown by its letter (A, B, C…) or with the button on its card.') })
      return
    }
    void ask(question.trim(), history)
  }

  /** Sends the failed question again. Only the last message can be retried, so its context is exact. */
  const retry = (failed: ChatMessage) => {
    if (!failed.retryQuestion || busy || messages[messages.length - 1]?.id !== failed.id) return
    const rest = messages.slice(0, -1)
    setMessages(rest)
    void ask(failed.retryQuestion, historyOf(rest.slice(0, -1)))
  }

  const restart = () => {
    setMessages([welcome()])
    setSearchContext(null)
    setPendingClarification(null)
    setSelected(null)
    setDraft('')
    inputRef.current?.focus()
  }

  const continueToBooking = () => {
    const card = selected?.card
    if (!card?.boardingStationId || !card.disembarkingStationId) return
    dispatch(
      setQuery({
        originId: card.boardingStationId,
        destinationId: card.disembarkingStationId,
        dateId: toVietnamDate(new Date(card.departureTime)),
      }),
    )
    dispatch(selectTrip(card.tripId))
    setOpen(false)
    navigate(ROUTES.tripDetail)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    send(draft)
  }

  // Enter sends, Shift+Enter starts a new line (not while an IME is composing a word).
  const onInputKey = (e: ReactKeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      send(draft)
    }
  }

  const fresh = messages.length === 1

  // Above the site header (z-50) while open: on a phone the panel covers the whole screen.
  return (
    <div className={`fixed bottom-5 right-5 ${open ? 'z-[60]' : 'z-40'} flex flex-col items-end gap-3 print:hidden`}>
      {open && (
        <section
          role="dialog"
          aria-label={t('Smart Waterbus Assistant')}
          className="fixed inset-0 z-50 flex flex-col overflow-hidden bg-white sm:static sm:inset-auto sm:h-[min(40rem,calc(100vh-7rem))] sm:w-[24rem] sm:rounded-2xl sm:border sm:border-outline-variant/40 sm:shadow-[0_12px_40px_rgba(13,37,56,0.18)]"
        >
          <header className="flex items-center gap-3 bg-deep-river px-space-md py-3 text-on-primary">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/10 text-sky-aqua">
              <Icon name="bolt" className="text-[20px]" />
            </span>
            <div className="min-w-0 flex-1">
              <div className="font-headline-sm text-sm font-bold">{t('Smart Waterbus Assistant')}</div>
              <div className="truncate text-xs text-sand-light/80">{t('Find waterbus trips')}</div>
            </div>
            {!fresh && (
              <button
                type="button"
                onClick={restart}
                disabled={busy}
                aria-label={t('New conversation')}
                title={t('New conversation')}
                className="flex h-9 w-9 items-center justify-center rounded-full hover:bg-white/10 disabled:opacity-50"
              >
                <Icon name="refresh" className="text-[18px]" />
              </button>
            )}
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t('Close assistant')}
              className="flex h-9 w-9 items-center justify-center rounded-full hover:bg-white/10"
            >
              <Icon name="close" className="text-[20px]" />
            </button>
          </header>

          <div className="flex-1 space-y-3 overflow-y-auto overscroll-contain p-space-md" aria-live="polite">
            {messages.map((m, i) => (
              <MessageBubble
                key={m.id}
                message={m}
                onRetry={
                  m.tone === 'error' && m.retryQuestion && !busy && i === messages.length - 1
                    ? () => retry(m)
                    : undefined
                }
              >
                {m.apiCards && m.apiCards.length > 0 && (
                  <AssistantApiCards
                    cards={m.apiCards}
                    selectedId={selected?.card.tripId ?? null}
                    onSelect={(card) =>
                      selectCard(card, cardLetter(m.apiCards?.findIndex((c) => c.tripId === card.tripId) ?? 0))
                    }
                  />
                )}
              </MessageBubble>
            ))}
            {fresh && <Suggestions disabled={busy} onPick={(label, question) => send(label, question)} />}
            {busy && <TypingIndicator />}
            <div ref={endRef} />
          </div>

          {selected && (
            <div className="flex items-center gap-2 border-t border-teal-flow/20 bg-teal-flow/5 px-space-md py-2.5">
              <div className="min-w-0 flex-1 text-xs text-deep-river">
                <div className="font-semibold">
                  {t('Trip {letter} selected', { letter: selected.letter })}
                </div>
                <div className="truncate text-on-surface-variant">
                  {selected.card.departureStationName} → {selected.card.arrivalStationName} ·{' '}
                  {cardDeparture(selected.card.departureTime).time}
                </div>
              </div>
              <button
                type="button"
                onClick={continueToBooking}
                className="inline-flex min-h-[40px] shrink-0 items-center gap-1 rounded-full bg-teal-flow px-4 text-sm font-semibold text-white hover:bg-secondary focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow"
              >
                {t('Continue to booking')}
                <Icon name="arrow_forward" className="text-[16px]" />
              </button>
            </div>
          )}

          <form onSubmit={submit} className="border-t border-surface-container p-space-sm">
            <div className="flex items-end gap-2 rounded-xl bg-mist p-1.5 focus-within:ring-2 focus-within:ring-teal-flow/30">
              <textarea
                ref={inputRef}
                rows={1}
                value={draft}
                maxLength={MAX_MESSAGE_LENGTH}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={onInputKey}
                placeholder={t('Type your question...')}
                aria-label={t('Ask the assistant')}
                className="max-h-28 min-h-[40px] flex-1 resize-none rounded-lg border-0 bg-transparent px-2 py-2 text-sm text-deep-river placeholder:text-outline focus:ring-0"
              />
              <button
                type="submit"
                disabled={busy || !draft.trim()}
                aria-label={t('Send')}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-teal-flow text-white transition-colors hover:bg-secondary disabled:bg-outline-variant/60"
              >
                <Icon name="send" className="text-[18px]" />
              </button>
            </div>
            <p className="mt-1.5 px-1 text-[11px] leading-snug text-outline">
              {t('Trips come from the live timetable. Check the time and price before you pay.')}
            </p>
          </form>
        </section>
      )}

      <button
        ref={launcherRef}
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-label={open ? t('Close assistant') : t('Open assistant')}
        className={`${open ? 'hidden sm:flex' : 'flex'} h-14 w-14 items-center justify-center rounded-full bg-teal-flow text-on-primary shadow-[0_6px_20px_rgba(20,122,126,0.35)] transition-all hover:bg-secondary active:scale-95`}
      >
        <Icon name={open ? 'close' : 'chat'} className="text-[26px]" />
      </button>
    </div>
  )
}
