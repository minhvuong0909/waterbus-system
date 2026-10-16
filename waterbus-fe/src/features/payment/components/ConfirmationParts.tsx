import { Link } from 'react-router-dom'
import { CopyButton, Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { NextStep } from '../types'
import { t } from '../../../i18n'

const CHIPS = ['Trip', 'Seat', 'Details', 'Checkout']

/** Slim bar under the breadcrumb: four ticks and the highlighted "Confirmed" step. */
export function ConfirmationSteps() {
  return (
    <div className="flex flex-wrap items-center gap-2 text-[11px] font-semibold text-teal-flow">
      {CHIPS.map((label, i) => (
        <span key={label} className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1 rounded-full bg-sand-light px-2.5 py-1">
            <Icon name="check_circle" className="text-[13px]" />
            {String(i + 1).padStart(2, '0')} {t(label)}
          </span>
          <span className="hidden h-px w-3 bg-outline-variant sm:inline-block" />
        </span>
      ))}
      <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-1 text-on-secondary-container ring-1 ring-teal-flow/30">
        <Icon name="check_circle" className="text-[13px]" />
        {t('05 Confirmed')}
      </span>
    </div>
  )
}

export function ConfirmationHero({ reference, email }: { reference: string; email: string }) {
  return (
    <div>
      <div className="max-w-2xl space-y-space-md">
        <span className="inline-flex items-center gap-2 rounded-full bg-secondary-container/70 px-3 py-1 text-xs font-semibold text-on-secondary-container">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Payment Received • Booking Confirmed')}
        </span>
        <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river md:text-headline-xl">
          {t('Your Journey Is Confirmed')}
        </h1>
        <p className="max-w-lg text-body-lg text-on-surface-variant">
          {t(
            'Your river crossing is set. Sit back, take in the Saigon skyline, and enjoy a quiet, smooth ride across the central corridor.',
          )}
        </p>
        <div className="inline-flex items-center gap-3 rounded-xl border border-outline-variant/40 bg-white px-space-md py-2 shadow-sm">
          <span className="text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Booking Ref:')}
          </span>
          <span className="font-headline-sm text-base font-bold text-deep-river">{reference}</span>
          <CopyButton value={reference} />
        </div>
        <p className="text-sm text-on-surface-variant">
          {t('We emailed the confirmation and a secure link to manage this booking to {email}.', {
            email,
          })}
        </p>
      </div>
    </div>
  )
}

export function QuickActions() {
  const base =
    'flex flex-1 items-center justify-center rounded-lg px-3 py-3 text-center text-xs font-semibold transition-colors'
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <div className="mb-space-sm text-[11px] font-bold uppercase tracking-wider text-on-surface-variant">
        {t('Quick Actions')}
      </div>
      <div className="flex flex-col gap-space-sm sm:flex-row">
        <Link
          to={ROUTES.myTickets}
          className={`${base} bg-teal-flow text-on-primary hover:bg-secondary`}
        >
          {t('View My Ticket')}
        </Link>
        <Link
          to={ROUTES.liveTracking}
          className={`${base} bg-surface-container text-deep-river hover:bg-surface-container-high`}
        >
          {t('Track This Trip')}
        </Link>
      </div>
    </div>
  )
}

export function WhatsNext({ steps }: { steps: NextStep[] }) {
  return (
    <section className="bg-sand-light/50 py-space-2xl">
      <div className="mx-auto max-w-7xl px-margin">
        <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Seamless Transit')}
        </span>
        <h2 className="mb-space-lg font-headline-lg text-headline-lg tracking-tight text-deep-river">
          {t('What Happens Next?')}
        </h2>
        <div className="grid grid-cols-1 gap-space-md md:grid-cols-3">
          {steps.map((s) => (
            <div
              key={s.number}
              className="rounded-2xl bg-white p-space-lg shadow-[0_2px_12px_rgba(13,37,56,0.05)]"
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-mist font-headline-sm text-sm font-bold text-teal-flow">
                {s.number}
              </span>
              <h3 className="mt-space-md font-headline-sm text-lg font-bold text-deep-river">
                {s.title}
              </h3>
              <p className="mt-1 text-sm text-on-surface-variant">{s.description}</p>
              <span className="mt-space-md inline-flex items-center gap-1.5 text-xs font-semibold text-teal-flow">
                <Icon name={s.chipIcon} className="text-[16px]" />
                {s.chipLabel}
              </span>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
