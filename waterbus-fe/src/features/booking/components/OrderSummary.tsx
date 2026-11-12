import type { ReactNode } from 'react'
import { formatVnd } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import type { BookingTotals } from '../utils'
import { seatCodes } from '../utils'
import BookingCard from './BookingCard'
import SummaryRow from './SummaryRow'
import SummaryTimer from './SummaryTimer'
import TotalRow from './TotalRow'
import { t } from '../../../i18n'

interface Leg {
  trip: TripDetail
  seats: Seat[]
}

/** One leg: departure and arrival with their times, then the day and the length of the run. */
function LegRoute({ trip, label }: { trip: TripDetail; label?: string }) {
  return (
    <div className="rounded-xl bg-mist/70 p-space-md">
      {label && (
        <div className="mb-2 text-2xs font-bold uppercase tracking-wider text-teal-flow">{label}</div>
      )}
      <ol className="space-y-2 text-sm">
        <li className="flex items-baseline gap-3">
          <span className="w-11 shrink-0 font-display font-bold text-deep-river">{trip.departTime}</span>
          <span className="text-deep-river">{trip.originPierLabel}</span>
        </li>
        <li className="flex items-baseline gap-3">
          <span className="w-11 shrink-0 font-display font-bold text-deep-river">{trip.arriveTime}</span>
          <span className="text-deep-river">{trip.destinationPierLabel}</span>
        </li>
      </ol>
      <p className="mt-2 border-t border-outline-variant/50 pt-2 text-xs text-on-surface-variant">
        {trip.dateFull} • {t('{durationMins} min', { durationMins: trip.durationMins })}
      </p>
    </div>
  )
}

/**
 * The order so far, the same card on every booking step: the trip (both legs of a round trip),
 * the seats, the buyer once known and the total. Each step passes its own action as `children`.
 * It stays in view while the page scrolls and carries the seat-hold countdown.
 */
export default function OrderSummary({
  trip,
  seats,
  totals,
  returnLeg,
  buyerName,
  countdownFallback,
  children,
}: {
  trip: TripDetail
  seats: Seat[]
  /** Covers both legs for a round trip (one order, one payment: BR-COM-02). */
  totals: BookingTotals
  returnLeg?: Leg
  buyerName?: string
  /** Shown instead of the countdown while no seats are held yet. */
  countdownFallback?: ReactNode
  children: ReactNode
}) {
  return (
    <BookingCard className="lg:sticky lg:top-[5.5rem] lg:max-h-[calc(100svh-6.5rem)] lg:overflow-y-auto lg:overscroll-contain">
      <h2 className="mb-space-md font-display text-headline-sm text-deep-river">{t('Order Summary')}</h2>

      <SummaryTimer fallback={countdownFallback} />

      <div className="space-y-2">
        <LegRoute trip={trip} label={returnLeg ? t('Outbound trip') : undefined} />
        {returnLeg && <LegRoute trip={returnLeg.trip} label={t('Return trip')} />}
      </div>

      <div className="mt-space-md space-y-1.5">
        {seats.length === 0 ? (
          <p className="text-sm text-on-surface-variant">{t('No seat chosen yet. Tap a seat on the map.')}</p>
        ) : (
          <SummaryRow
            label={returnLeg ? t('Outbound seats') : t('Selected Seats')}
            value={<span className="font-semibold">{seatCodes(seats)}</span>}
          />
        )}
        {returnLeg && (
          <SummaryRow
            label={t('Return seats')}
            value={<span className="font-semibold">{seatCodes(returnLeg.seats)}</span>}
          />
        )}
        {buyerName && <SummaryRow label={t('Buyer')} value={buyerName} />}
        {totals.seatCount > 0 && (
          <SummaryRow
            label={
              totals.seatCount > 1
                ? t('Seat fares ({count} seats)', { count: totals.seatCount })
                : t('Seat fare')
            }
            value={formatVnd(totals.totalVnd)}
          />
        )}
      </div>

      <div className="mt-space-sm border-t border-surface-container">
        <TotalRow
          label={t('Total Amount')}
          caption={t('All taxes & fees included')}
          amountVnd={totals.totalVnd}
        />
      </div>

      {children}
    </BookingCard>
  )
}
