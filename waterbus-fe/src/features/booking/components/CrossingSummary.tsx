import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import type { BookingTotals } from '../utils'
import { passengersLabel, selectedSeatsLabel } from '../utils'
import BookingCard from './BookingCard'
import SummaryRow from './SummaryRow'
import TotalRow from './TotalRow'
import SummaryTimer from './SummaryTimer'
import { t } from '../../../i18n'

interface CrossingSummaryProps {
  trip: TripDetail
  seats: Seat[]
  totals: BookingTotals
  /** False until the buyer and every seat's passenger are filled in correctly. */
  canContinue: boolean
  /** The return leg of a round trip; `totals` then covers both legs. */
  returnLeg?: { trip: TripDetail; seats: Seat[] }
}

/** Departure and arrival of one leg, with the river run between them. */
function LegTimeline({ trip, label }: { trip: TripDetail; label?: string }) {
  return (
    <div className="rounded-xl bg-mist/70 p-space-md">
      <div className="mb-3 flex items-center justify-between text-xs">
        <span className="font-bold uppercase tracking-wider text-teal-flow">{label ?? trip.lineLabel}</span>
        <span className="text-on-surface-variant">{trip.dateFull}</span>
      </div>
      <div className="flex gap-3">
        <div className="flex flex-col items-center">
          <span className="h-3 w-3 rounded-full bg-teal-flow" />
          <span className="my-1 w-0.5 flex-1 bg-teal-flow/40" />
          <span className="h-3 w-3 rounded-full bg-coral-glow" />
        </div>
        <div className="flex-1 space-y-2.5 text-sm">
          <div className="flex justify-between">
            <div className="font-semibold text-deep-river">{trip.originPierLabel}</div>
            <span className="font-semibold text-deep-river">{trip.departTime}</span>
          </div>
          <span className="inline-flex items-center gap-1 rounded-full bg-white px-2.5 py-1 text-xs text-teal-flow shadow-sm">
            <Icon name="directions_boat" className="text-[14px]" />
            {t('{durationMins} min river run', { durationMins: trip.durationMins })}
          </span>
          <div className="flex justify-between">
            <div className="font-semibold text-deep-river">{trip.destinationPierLabel}</div>
            <span className="font-semibold text-deep-river">{trip.arriveTime}</span>
          </div>
        </div>
      </div>
    </div>
  )
}

/** Sticky sidebar on passenger details: timeline, vessel facts and fare lines. */
export default function CrossingSummary({ trip, seats, totals, canContinue, returnLeg }: CrossingSummaryProps) {
  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-[5.5rem] lg:max-h-[calc(100svh-6.5rem)] lg:overflow-y-auto lg:overscroll-contain">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">
          {t('Crossing Summary')}
        </h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <SummaryTimer />

      <div className="space-y-2">
        <LegTimeline trip={trip} label={returnLeg ? t('Outbound trip') : undefined} />
        {returnLeg && <LegTimeline trip={returnLeg.trip} label={t('Return trip')} />}
      </div>

      <div className="mt-space-md space-y-2 rounded-xl bg-sand-light/60 p-space-md">
        <SummaryRow
          label={
            returnLeg ? t('Outbound seats') : seats.length > 1 ? t('Selected Seats') : t('Selected Seat')
          }
          value={selectedSeatsLabel(seats)}
          accent
        />
        {returnLeg && (
          <SummaryRow label={t('Return seats')} value={selectedSeatsLabel(returnLeg.seats)} accent />
        )}
        <SummaryRow label={t('Passengers')} value={passengersLabel(seats.length)} />
      </div>

      <div className="mt-space-md space-y-1.5">
        <SummaryRow
          label={
            totals.seatCount > 1
              ? t('Seat fares ({count} seats)', { count: totals.seatCount })
              : t('Seat fare')
          }
          value={formatVnd(totals.totalVnd)}
        />
      </div>
      <div className="mt-space-sm border-t border-surface-container">
        <TotalRow
          label={t('Total Amount')}
          caption={t('All taxes & fees included')}
          amountVnd={totals.totalVnd}
        />
      </div>

      {canContinue ? (
        <Link
          to={ROUTES.checkoutReview}
          className="group mt-space-sm flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary"
        >
          {t('Continue to Checkout')}
          <Icon
            name="arrow_forward"
            className="text-[18px] transition-transform group-hover:translate-x-1"
          />
        </Link>
      ) : (
        <>
          <button
            type="button"
            disabled
            className="mt-space-sm flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary opacity-50"
          >
            {t('Continue to Checkout')}
            <Icon name="arrow_forward" className="text-[18px]" />
          </button>
          <p role="status" className="mt-2 text-center text-xs text-on-surface-variant">
            {t('Enter a name for every passenger and the buyer’s name and email to continue.')}
          </p>
        </>
      )}
      <div className="mt-space-sm text-center">
        <Link
          to={ROUTES.seatSelection}
          className="text-sm font-semibold text-teal-flow hover:underline"
        >
          {returnLeg ? t('Change outbound seats') : t('Change Seat')}
        </Link>
        {returnLeg && (
          <Link
            to={`${ROUTES.seatSelection}?leg=return`}
            className="ml-space-md text-sm font-semibold text-teal-flow hover:underline"
          >
            {t('Change return seats')}
          </Link>
        )}
      </div>
    </BookingCard>
  )
}
