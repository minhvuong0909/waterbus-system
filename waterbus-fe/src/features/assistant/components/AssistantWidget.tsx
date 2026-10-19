import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip, setQuery } from '../../booking/bookingSlice'
import type { Trip } from '../../trips/types'
import { askAssistant } from '../services/assistantService'
import type { AssistantApiCard, ChatMessage } from '../types'
import AssistantTripCard from './AssistantTripCard'
import AssistantApiCards from './AssistantApiCards'
import { t } from '../../../i18n'

const CHIPS = [
  { label: 'Next departures', question: 'Show the next departures' },
  { label: 'Refund Help', question: 'Refund Help' },
  { label: 'Trip Status', question: 'Trip Status' },
]

/** Chat bubble in the corner of every page. The assistant can answer with trip cards. */
export default function AssistantWidget() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [draft, setDraft] = useState('')
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 1,
      from: 'assistant',
      text: t('Hi! I can help with departures and refunds. What would you like to know?'),
    },
  ])
  const inputRef = useRef<HTMLInputElement>(null)
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (open) inputRef.current?.focus()
  }, [open])

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ block: 'end' })
  }, [messages, busy])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setOpen(false)
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open])

  const send = async (text: string, question = text) => {
    const clean = text.trim()
    if (!clean || busy) return
    // Chỉ gửi các lượt thực sự trao đổi, tối đa 8 để không phình prompt hoặc lưu trên server.
    const history = messages.slice(1).slice(-8).map((m) => ({ role: m.from, content: m.text }))
    setMessages((m) => [...m, { id: m.length + 1, from: 'user', text: clean }])
    setDraft('')
    setBusy(true)
    let reply: ChatMessage
    try {
      const answer = await askAssistant(question, { history })
      reply = { id: 0, from: 'assistant', ...answer }
    } catch {
      reply = {
        id: 0,
        from: 'assistant',
        text: t('Sorry, I couldn’t reach the assistant. Please try again in a moment.'),
      }
    }
    setMessages((m) => [...m, { ...reply, id: m.length + 1 }])
    setBusy(false)
  }

  const chooseTrip = (trip: Trip, search: ChatMessage['search']) => {
    if (search) dispatch(setQuery(search))
    dispatch(selectTrip(trip))
    setOpen(false)
    navigate(ROUTES.tripDetail)
  }

  const chooseApiCard = (card: AssistantApiCard) => {
    if (!card.boardingStationId || !card.disembarkingStationId) return
    const dateId = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
      .format(new Date(card.departureTime))
    dispatch(setQuery({
      originId: card.boardingStationId,
      destinationId: card.disembarkingStationId,
      dateId,
    }))
    dispatch(selectTrip(card.tripId))
    setOpen(false)
    navigate(ROUTES.tripDetail)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    send(draft)
  }

  return (
    <div className="fixed bottom-5 right-5 z-40 flex flex-col items-end gap-3 print:hidden">
      {open && (
        <section
          role="dialog"
          aria-label={t('Smart Waterbus Assistant')}
          className="flex max-h-[min(34rem,calc(100vh-7rem))] w-[22rem] max-w-[calc(100vw-2.5rem)] flex-col overflow-hidden rounded-2xl border border-outline-variant/40 bg-white shadow-[0_12px_40px_rgba(13,37,56,0.18)]"
        >
          <header className="flex items-center gap-3 bg-deep-river px-space-md py-3 text-on-primary">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/10 text-sky-aqua">
              <Icon name="bolt" className="text-[20px]" />
            </span>
            <div className="flex-1">
              <div className="font-headline-sm text-sm font-bold">
                {t('Smart Waterbus Assistant')}
              </div>
              <div className="text-[11px] text-sand-light/80">{t('Ask about trips and tickets')}</div>
            </div>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t('Close assistant')}
              className="flex h-8 w-8 items-center justify-center rounded-full hover:bg-white/10"
            >
              <Icon name="close" className="text-[18px]" />
            </button>
          </header>

          <div className="flex-1 space-y-2 overflow-y-auto p-space-md" aria-live="polite">
            {messages.map((m) => (
              <div key={m.id} className={m.from === 'user' ? 'flex justify-end' : ''}>
                <div
                  className={`inline-block max-w-[90%] rounded-xl px-3 py-2 text-xs ${
                    m.from === 'user'
                      ? 'bg-surface-container text-deep-river'
                      : 'bg-mist text-on-surface-variant'
                  }`}
                >
                  {m.text}
                </div>
                {m.apiCards && <AssistantApiCards cards={m.apiCards} onSelect={chooseApiCard} />}
                {m.trips && (
                  <div className="mt-2 space-y-2">
                    {m.trips.map((trip) => (
                      <AssistantTripCard
                        key={trip.id}
                        trip={trip}
                        onSelect={(chosen) => chooseTrip(chosen, m.search)}
                      />
                    ))}
                  </div>
                )}
              </div>
            ))}
            {busy && (
              <div className="text-xs text-on-surface-variant">{t('Assistant is typing…')}</div>
            )}
            <div ref={endRef} />
          </div>

          <div className="flex flex-wrap gap-1.5 border-t border-surface-container px-space-md pt-space-sm">
            {CHIPS.map((chip) => (
              <button
                key={chip.label}
                type="button"
                onClick={() => send(t(chip.label), chip.question)}
                className="rounded-full border border-outline-variant/50 bg-white px-2.5 py-1 text-[11px] text-deep-river transition-colors hover:bg-surface-container-low"
              >
                {t(chip.label)}
              </button>
            ))}
          </div>

          <form onSubmit={submit} className="m-space-sm flex items-center gap-2 rounded-xl bg-mist p-1.5">
            <input
              ref={inputRef}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              placeholder={t('Type your question...')}
              aria-label={t('Ask the assistant')}
              className="flex-1 rounded-lg border-0 bg-transparent px-2 py-1.5 text-xs text-deep-river placeholder:text-outline focus:ring-0"
            />
            <button
              type="submit"
              disabled={busy}
              aria-label={t('Send')}
              className="flex h-8 w-8 items-center justify-center text-teal-flow hover:text-deep-river disabled:opacity-50"
            >
              <Icon name="send" className="text-[18px]" />
            </button>
          </form>
        </section>
      )}

      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-label={open ? t('Close assistant') : t('Open assistant')}
        className="flex h-14 w-14 items-center justify-center rounded-full bg-teal-flow text-on-primary shadow-[0_6px_20px_rgba(20,122,126,0.35)] transition-all hover:bg-secondary active:scale-95"
      >
        <Icon name={open ? 'close' : 'chat'} className="text-[26px]" />
      </button>
    </div>
  )
}
