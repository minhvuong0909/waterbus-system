import { Link } from 'react-router-dom'
import waterwayImage from '../../../assets/images/waterway.jpg'
import vesselImage from '../../../assets/images/vessel.jpg'
import { CopyButton, Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TripDetail } from '../../trips/types'
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

export function ConfirmationHero({ reference, trip }: { reference: string; trip: TripDetail }) {
  return (
    <div className="grid grid-cols-1 items-center gap-space-xl lg:grid-cols-2">
      <div className="space-y-space-md">
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
      </div>
      <div className="relative overflow-hidden rounded-2xl shadow-xl">
        <img
          alt={t('Smart Waterbus electric catamaran gliding on the Saigon River at golden hour')}
          src={waterwayImage}
          className="h-72 w-full object-cover md:h-80"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-deep-river/70 via-transparent to-transparent" />
        <div className="absolute inset-x-space-md bottom-space-md flex items-end justify-between text-white">
          <div>
            <div className="font-headline-sm text-sm font-semibold text-sky-aqua">
              {t('Smart Waterbus')}
            </div>
            <div className="text-xs">{t('Central River Crossing')}</div>
          </div>
          <span className="inline-flex items-center gap-1 rounded-full bg-deep-river/80 px-3 py-1 text-xs font-semibold backdrop-blur">
            <Icon name="directions_boat" className="text-[14px]" />
            {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
          </span>
        </div>
      </div>
    </div>
  )
}

export function CorridorCard({ trip }: { trip: TripDetail }) {
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">
          {t('River Crossing Corridor')}
        </h2>
        <span className="rounded bg-secondary-container px-2 py-0.5 text-[10px] font-bold text-teal-flow">
          {t('{vesselCode} Route', { vesselCode: trip.vesselCode })}
        </span>
      </div>
      <div className="relative h-52 overflow-hidden rounded-xl bg-[#E0F2F5]">
        <svg
          className="absolute inset-0 h-full w-full"
          viewBox="0 0 400 200"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path
            d="M0 95 C 90 70 150 130 220 105 C 300 78 340 105 400 92 L 400 150 C 330 165 290 130 220 155 C 150 178 90 120 0 150 Z"
            fill="#4FC3D8"
            opacity="0.35"
          />
          <path
            d="M0 122 C 90 98 150 158 220 130 C 300 104 340 130 400 118"
            fill="none"
            stroke="#147A7E"
            strokeWidth="3"
            strokeDasharray="7 7"
            strokeLinecap="round"
          />
        </svg>
        <span className="absolute left-3 top-3 flex items-center gap-1.5 rounded-full bg-white/95 px-2.5 py-1 text-[11px] font-semibold text-deep-river shadow-sm">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {trip.originPierLabel}
        </span>
        <span className="absolute bottom-3 right-3 flex items-center gap-1.5 rounded-full bg-white/95 px-2.5 py-1 text-[11px] font-semibold text-deep-river shadow-sm">
          <span className="h-2 w-2 rounded-full bg-deep-river" />
          {trip.destinationPierLabel}
        </span>
        <span className="absolute left-1/2 top-1/2 flex -translate-x-1/2 -translate-y-1/2 items-center gap-1.5 rounded-full bg-deep-river px-3 py-1.5 text-[11px] font-bold text-white shadow-lg">
          <Icon name="directions_boat" className="text-[14px]" />
          {t('Waterbus {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>
      <div className="mt-space-md flex items-center justify-between rounded-lg bg-mist px-space-md py-2 text-xs text-on-surface-variant">
        <span className="flex items-center gap-2">
          <Icon name="schedule" className="text-[16px] text-teal-flow" />
          {t('Scheduled crossing: {durationMins} minutes', { durationMins: trip.durationMins })}
        </span>
        <span className="font-semibold text-teal-flow">{t('Direct Line')}</span>
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
        <Link
          to={ROUTES.explore}
          className={`${base} bg-surface-container text-deep-river hover:bg-surface-container-high`}
        >
          {t('Explore Along the Route')}
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

export function WaterwayBanner() {
  return (
    <div className="grid grid-cols-1 overflow-hidden rounded-2xl bg-white shadow-[0_2px_16px_rgba(13,37,56,0.06)] md:grid-cols-2">
      <img
        alt={t('Scenic Saigon River waterfront promenade with river parks and skyline')}
        src={vesselImage}
        className="h-64 w-full object-cover md:h-full"
      />
      <div className="flex flex-col justify-center gap-space-sm p-space-xl">
        <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Waterway Experience')}
        </span>
        <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
          {t('Discover Along the Waterway')}
        </h2>
        <p className="text-sm text-on-surface-variant">
          {t(
            'Explore scenic waterfront promenades, river parks, and cultural viewpoints along your route.',
          )}
        </p>
        <Link
          to={ROUTES.explore}
          className="mt-space-sm inline-flex w-fit items-center gap-2 rounded-lg bg-teal-flow px-5 py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('Explore Along the Route')}
          <Icon name="arrow_forward" className="text-[18px]" />
        </Link>
      </div>
    </div>
  )
}
