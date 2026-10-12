import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import { addMinutes } from '../../../utils/time'
import { askAssistant, requestRefund } from '../services/ticketService'
import type { ChatMessage, ManageBooking, Ticket } from '../types'
import { t } from '../../../i18n'

export type ManageOption = 'change' | 'refund' | 'voucher'

const OPTIONS: {
  id: ManageOption
  icon: string
  title: string
  description: string
  cta: string
}[] = [
  {
    id: 'change',
    icon: 'swap_horiz',
    title: 'Change Trip',
    description: 'Choose another available trip.',
    cta: 'Change Trip',
  },
  {
    id: 'refund',
    icon: 'credit_card',
    title: 'Request Refund',
    description: 'Submit a refund request.',
    cta: 'Request Refund',
  },
  {
    id: 'voucher',
    icon: 'paid',
    title: 'Voucher / Credit',
    description: 'View voucher or credit associated with this booking.',
    cta: 'View Voucher',
  },
]

export function BookingSummaryCard({ ticket }: { ticket: Ticket }) {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <div className="flex flex-wrap items-center justify-between gap-space-md">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-deep-river text-center font-headline-sm text-xs font-bold leading-tight text-white">
            {ticket.tripCode}
          </span>
          <div>
            <div className="text-[11px] font-semibold uppercase tracking-wider text-teal-flow">
              {t('Trip {tripCode}', { tripCode: ticket.tripCode })}
            </div>
            <div className="font-headline-sm text-xl font-bold text-deep-river">
              {ticket.departPier} → {ticket.arrivePier}
            </div>
          </div>
        </div>
        <div className="flex gap-space-sm">
          <Link
            to={ROUTES.ticketDetail}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
            <Icon name="confirmation_number" className="text-[16px]" />
            {t('View Ticket')}
          </Link>
          <Link
            to={ROUTES.liveTracking}
            className="inline-flex items-center gap-1.5 rounded-lg border border-outline-variant/50 bg-white px-4 py-2 text-xs font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
          >
            <Icon name="map" className="text-[16px]" />
            {t('Track Trip')}
          </Link>
        </div>
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-center gap-space-md border-t border-surface-container pt-space-lg md:grid-cols-12">
        <div className="md:col-span-4">
          <div className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Departure')}
          </div>
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
            {ticket.departPier}
          </div>
          <div className="font-headline-sm text-lg font-semibold text-teal-flow">
            {ticket.departTime}
          </div>
        </div>
        <div className="flex flex-col items-center md:col-span-4">
          <span className="text-xs text-teal-flow">
            {t('{durationMins} min crossing', { durationMins: ticket.durationMins })}
          </span>
          <div className="mt-1 flex w-full items-center">
            <span className="h-2.5 w-2.5 rounded-full border-2 border-teal-flow bg-white" />
            <span className="h-1 flex-1 rounded-full bg-gradient-to-r from-teal-flow to-sky-aqua" />
            <span className="h-2.5 w-2.5 rounded-full bg-teal-flow" />
          </div>
        </div>
        <div className="md:col-span-4 md:text-right">
          <div className="text-[11px] font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Arrival')}
          </div>
          <div className="font-headline-sm text-2xl font-bold text-deep-river">
            {ticket.arrivePier}
          </div>
          <div className="font-headline-sm text-lg font-semibold text-teal-flow">
            {ticket.arriveTime}
          </div>
        </div>
      </div>

      <dl className="mt-space-lg grid grid-cols-2 gap-space-md border-t border-surface-container pt-space-md md:grid-cols-4">
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Seat')}</dt>
          <dd className="font-semibold text-deep-river">{ticket.seat}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Passenger')}</dt>
          <dd className="font-semibold text-deep-river">{t('1 Passenger')}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Fare')}</dt>
          <dd className="font-semibold text-deep-river">{formatVnd(ticket.fareVnd)}</dd>
        </div>
        <div>
          <dt className="text-xs text-on-surface-variant">{t('Status')}</dt>
          <dd className="font-semibold text-teal-flow">{ticket.status}</dd>
        </div>
      </dl>
    </div>
  )
}

export function OperatingBanner({ vessel }: { vessel: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-teal-flow/25 bg-secondary-container/40 px-space-md py-space-md text-sm">
      <span className="flex h-8 w-8 items-center justify-center rounded-full bg-teal-flow text-on-primary">
        <Icon name="check" className="text-[18px]" />
      </span>
      <span>
        <b className="text-deep-river">{t('Trip Operating Normally.')}</b>{' '}
        <span className="text-teal-flow">
          {t('Vessel {vessel} is operating on schedule.', { vessel })}
        </span>
      </span>
    </div>
  )
}

export function ManageOptions({
  active,
  onSelect,
}: {
  active: ManageOption
  onSelect: (o: ManageOption) => void
}) {
  return (
    <div>
      <h2 className="font-headline-lg text-headline-md text-deep-river">{t('Manage Your Trip')}</h2>
      <p className="text-sm text-on-surface-variant">
        {t('Choose a support option for this booking.')}
      </p>
      <div className="mt-space-md grid grid-cols-1 gap-space-md md:grid-cols-3">
        {OPTIONS.map((o) => {
          const on = active === o.id
          return (
            <div
              key={o.id}
              className={`rounded-2xl border-2 bg-white p-space-md shadow-sm transition-all ${
                on ? 'border-teal-flow shadow-md' : 'border-transparent'
              }`}
            >
              <div className="flex items-start justify-between">
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-mist text-teal-flow">
                  <Icon name={o.icon} className="text-[20px]" />
                </span>
                {on && <span className="h-2 w-2 rounded-full bg-teal-flow" />}
              </div>
              <div className="mt-space-sm font-headline-sm text-base font-bold text-deep-river">
                {t(o.title)}
              </div>
              <p className="text-xs text-on-surface-variant">{t(o.description)}</p>
              <button
                type="button"
                onClick={() => onSelect(o.id)}
                aria-pressed={on}
                className={`mt-space-md w-full rounded-lg py-2.5 text-sm font-semibold transition-colors ${
                  on
                    ? 'bg-teal-flow text-on-primary hover:bg-secondary'
                    : 'border border-outline-variant/50 bg-white text-deep-river hover:bg-surface-container-low'
                }`}
              >
                {t(o.cta)}
              </button>
            </div>
          )
        })}
      </div>
    </div>
  )
}

export function OptionPanel({ option, manage }: { option: ManageOption; manage: ManageBooking }) {
  const [refundState, setRefundState] = useState<'idle' | 'busy' | 'done' | 'failed'>('idle')
  const ticket = manage.ticket

  const submitRefund = async () => {
    setRefundState('busy')
    try {
      await requestRefund(manage.bookingCode)
      setRefundState('done')
    } catch {
      setRefundState('failed')
    }
  }

  const shell = 'rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]'

  if (option === 'refund') {
    return (
      <div className={shell}>
        <h3 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Refund Request')}
        </h3>
        <p className="mt-space-sm text-sm text-on-surface-variant">
          {t(
            'Refund {amount} for booking {bookingCode} ({seat}). Refunds are available up to 30 minutes before departure. Requests need your confirmation.',
            {
              amount: formatVnd(ticket.fareVnd),
              bookingCode: manage.bookingCode,
              seat: ticket.seat,
            },
          )}
        </p>
        <div className="mt-space-md flex items-center justify-between border-t border-surface-container pt-space-md">
          <span
            role={refundState === 'failed' ? 'alert' : 'status'}
            className={`text-xs ${refundState === 'failed' ? 'text-coral-glow' : 'text-teal-flow'}`}
          >
            {refundState === 'done' && t('Refund request submitted. We will email you an update.')}
            {refundState === 'failed' && t('We couldn’t submit your request. Please try again.')}
          </span>
          <button
            type="button"
            disabled={refundState === 'busy' || refundState === 'done'}
            onClick={submitRefund}
            className="rounded-lg bg-teal-flow px-5 py-2.5 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary disabled:opacity-60"
          >
            {refundState === 'busy'
              ? t('Submitting…')
              : refundState === 'done'
              ? t('Submitted')
              : refundState === 'failed'
              ? t('Try again')
              : t('Confirm Refund Request')}
          </button>
        </div>
      </div>
    )
  }

  if (option === 'voucher') {
    return (
      <div className={shell}>
        <h3 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Voucher / Credit')}
        </h3>
        <ul className="mt-space-sm space-y-space-sm">
          {manage.voucherCredit.map((v) => (
            <li
              key={v.code}
              className="flex items-center justify-between rounded-xl bg-mist/70 px-space-md py-space-sm text-sm"
            >
              <span>
                <span className="block font-mono text-xs font-semibold text-deep-river">
                  {v.code}
                </span>
                <span className="text-xs text-on-surface-variant">{v.description}</span>
              </span>
              <span className="font-semibold text-teal-flow">{formatVnd(v.valueVnd)}</span>
            </li>
          ))}
        </ul>
      </div>
    )
  }

  const route = `${ticket.departShort} → ${ticket.arriveShort}`
  // Suggested alternative: the same crossing 30 minutes later.
  const alt = {
    departTime: addMinutes(ticket.departTime, 30),
    arriveTime: addMinutes(ticket.arriveTime, 30),
  }
  return (
    <div className={shell}>
      <div className="flex items-center justify-between">
        <h3 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Change Trip Preview')}
        </h3>
        <span className="rounded-full bg-secondary-container px-3 py-0.5 text-[11px] font-semibold text-teal-flow">
          {t('Active Selection')}
        </span>
      </div>
      <div className="mt-space-md grid grid-cols-1 gap-space-md md:grid-cols-2">
        <div className="rounded-xl border border-outline-variant/50 bg-mist/60 p-space-md">
          <div className="text-[10px] font-bold uppercase tracking-wider text-teal-flow">
            {t('Current Trip')}
          </div>
          <div className="mt-1 font-headline-sm text-lg font-bold text-deep-river">
            {ticket.departTime} → {ticket.arriveTime}
          </div>
          <div className="text-xs text-on-surface-variant">{route}</div>
          <div className="mt-1 text-xs font-semibold text-teal-flow">{ticket.seat}</div>
        </div>
        <div className="rounded-xl border border-dashed border-teal-flow/50 bg-sand-light/40 p-space-md">
          <div className="text-[10px] font-bold uppercase tracking-wider text-teal-flow">
            {t('Suggested Alternative')}
          </div>
          <div className="mt-1 font-headline-sm text-lg font-bold text-deep-river">
            {alt.departTime} → {alt.arriveTime}
          </div>
          <div className="text-xs text-on-surface-variant">{route}</div>
          <div className="mt-1 text-xs text-on-surface-variant">{t('Seats available')}</div>
        </div>
      </div>
      <div className="mt-space-md flex items-center justify-between border-t border-surface-container pt-space-md">
        <span className="text-xs text-on-surface-variant">{t('Need another time today?')}</span>
        <Link
          to={ROUTES.searchResults}
          className="inline-flex items-center gap-1.5 rounded-lg bg-teal-flow px-5 py-2.5 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('View Available Trips')} <Icon name="arrow_forward" className="text-[16px]" />
        </Link>
      </div>
    </div>
  )
}

const CHIPS = ['Change Trip', 'Refund Help', 'Explain Voucher', 'Trip Status']

export function AssistantChat() {
  const [messages, setMessages] = useState<ChatMessage[]>([
    { id: 1, from: 'user', text: t('I need to change my trip.') },
    { id: 2, from: 'assistant', text: t('I can help you review available options.') },
  ])
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)

  const send = async (text: string, replyKey = text) => {
    const clean = text.trim()
    if (!clean || busy) return
    setMessages((m) => [...m, { id: m.length + 1, from: 'user', text: clean }])
    setDraft('')
    setBusy(true)
    let reply: string
    try {
      reply = await askAssistant(replyKey)
    } catch {
      reply = t('Sorry, I couldn’t reach the assistant. Please try again in a moment.')
    }
    setMessages((m) => [...m, { id: m.length + 1, from: 'assistant', text: reply }])
    setBusy(false)
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    send(draft)
  }

  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-deep-river text-sky-aqua">
          <Icon name="bolt" className="text-[22px]" />
        </span>
        <div>
          <div className="font-headline-sm text-sm font-bold uppercase tracking-wide text-deep-river">
            {t('Need Help?')}
          </div>
          <div className="text-xs text-on-surface-variant">
            {t('Ask Smart Waterbus Assistant about this booking.')}
          </div>
        </div>
      </div>

      <div className="mt-space-md space-y-2" aria-live="polite">
        {messages.map((m) => (
          <div key={m.id} className={`flex ${m.from === 'user' ? 'justify-end' : 'justify-start'}`}>
            <span
              className={`max-w-[85%] rounded-xl px-3 py-2 text-xs ${
                m.from === 'user'
                  ? 'bg-surface-container text-deep-river'
                  : 'bg-mist text-on-surface-variant'
              }`}
            >
              {m.text}
            </span>
          </div>
        ))}
        {busy && <div className="text-xs text-on-surface-variant">{t('Assistant is typing…')}</div>}
      </div>

      <div className="mt-space-md flex flex-wrap gap-2">
        {CHIPS.map((chip) => (
          <button
            key={chip}
            type="button"
            onClick={() => send(t(chip), chip)}
            className="rounded-full border border-outline-variant/50 bg-white px-3 py-1 text-xs text-deep-river transition-colors hover:bg-surface-container-low"
          >
            {t(chip)}
          </button>
        ))}
      </div>

      <form
        onSubmit={submit}
        className="mt-space-md flex items-center gap-2 rounded-xl bg-mist p-1.5"
      >
        <input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder={t('Ask about changing trips or refunds...')}
          aria-label={t('Ask the assistant')}
          className="flex-1 rounded-lg border-0 bg-transparent px-2 py-1.5 text-xs text-deep-river placeholder:text-outline focus:ring-0"
        />
        <button
          type="submit"
          aria-label={t('Send')}
          className="flex h-8 w-8 items-center justify-center text-teal-flow hover:text-deep-river"
        >
          <Icon name="arrow_forward" className="text-[18px]" />
        </button>
      </form>
      <p className="mt-2 text-center text-[11px] text-on-surface-variant">
        {t('Actions require passenger confirmation.')}
      </p>
    </div>
  )
}
