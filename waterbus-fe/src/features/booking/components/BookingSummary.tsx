import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd, formatVndSuffix } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import type { BookingFailure } from '../hooks/useCheckout'
import type { BookingTotals } from '../utils'
import { MAX_SEATS_PER_BOOKING } from '../utils'
import { hasSurcharge, seatFeeFor, seatPosition, seatZone } from '../utils'
import BookingCard from './BookingCard'
import SeatFeeRow from './SeatFeeRow'
import SummaryRow from './SummaryRow'
import TotalRow from './TotalRow'
import SummaryTimer from './SummaryTimer'
import { t } from '../../../i18n'

interface BookingSummaryProps {
  trip: TripDetail
  seats: Seat[]
  totals: BookingTotals
  /** True when no more seats can be added. */
  limitReached: boolean
  /** False when sales are closed for the trip. */
  canBook: boolean
  /** Holds the seats and moves on to the passenger details. */
  onContinue: () => void
  /** True while the hold is being made. */
  busy: boolean
  failure: BookingFailure | null
  /** Text of the main button; "Continue to Passenger Details" by default. */
  continueLabel?: string
  /** Where "Change Trip" leads: the results of this leg. */
  changeTripTo?: string
}

export default function BookingSummary({
  trip,
  seats,
  totals,
  limitReached,
  canBook,
  onContinue,
  busy,
  failure,
  continueLabel,
  changeTripTo = ROUTES.searchResults,
}: BookingSummaryProps) {
  const label = continueLabel ?? t('Continue to Passenger Details')
  const surcharge = seats.some(hasSurcharge)
  // An empty selection, or one with a seat somebody else took, must not reach checkout.
  const canContinue =
    canBook && seats.length > 0 && seats.every((seat) => seat.status === 'available')
  return (
    <BookingCard
      className="p-space-lg lg:sticky lg:top-[5.5rem] lg:max-h-[calc(100svh-6.5rem)] lg:overflow-y-auto lg:overscroll-contain"
    >
      <SummaryTimer
        fallback={
          <span className="inline-flex items-center gap-1 rounded-xl bg-surface-container px-space-md py-2 text-xs font-semibold text-teal-flow">
            <Icon name="timer" className="text-[15px]" /> {t('Cart holds for 10:00')}
          </span>
        }
      />
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">
          {t('Booking Summary')}
        </h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="space-y-1 rounded-xl border border-outline-variant/40 bg-mist/60 p-space-md">
        <div className="flex items-center justify-between gap-2 text-sm font-semibold text-deep-river">
          <span>{trip.originPierName}</span>
          <Icon name="arrow_forward" className="text-[16px] text-teal-flow" />
          <span>{trip.destinationPierName}</span>
        </div>
        <p className="text-xs text-on-surface-variant">
          {t('{date}, {time} Departure', { date: trip.dateLabel, time: trip.departTime })}
          {' • '}
          {t('{durationMins} min', { durationMins: trip.durationMins })}
        </p>
      </div>

      <div className="mt-space-md">
        <div className="mb-1.5 flex items-center justify-between text-[10px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Reserved Seat Choice')}
          <Icon name="airline_seat_recline_extra" className="text-[16px]" />
        </div>
        {seats.length === 0 ? (
          <p className="text-xs text-on-surface-variant">
            {t('No seat chosen yet. Tap a seat on the map.')}
          </p>
        ) : (
          <ul className="flex flex-wrap gap-1.5">
            {seats.map((seat) => {
              const priced = seat.priceVnd !== undefined
              const fee = priced ? (seat.priceVnd ?? 0) : seatFeeFor(seat, trip.fareVnd)
              return (
                <li
                  key={seat.id}
                  title={`${seatPosition(seat)} • ${seatZone(seat)}`}
                  className="inline-flex items-baseline gap-1.5 rounded-lg bg-sand-light px-2.5 py-1 text-sm font-bold text-deep-river"
                >
                  {seat.code}
                  {fee > 0 && (
                    <span className="text-[10px] font-medium text-teal-flow">
                      {priced ? formatVndSuffix(fee) : `+${formatVndSuffix(fee)}`}
                    </span>
                  )}
                </li>
              )
            })}
          </ul>
        )}
        {limitReached && (
          <p role="status" className="mt-2 text-xs text-coral-glow">
            {t('You can book up to {max} seats at a time.', { max: MAX_SEATS_PER_BOOKING })}
          </p>
        )}
      </div>

      <div className="mt-space-md space-y-1.5">
        <SummaryRow
          label={
            totals.seatCount > 1
              ? t('Seat fares ({count} seats)', { count: totals.seatCount })
              : t('Seat fare')
          }
          value={formatVnd(totals.fareVnd)}
        />
        <SeatFeeRow
          label={surcharge ? t('Cabin Surcharge') : t('Seat Reservation Fee')}
          seatFeeVnd={totals.seatFeeVnd}
          includedText={t('Included (0 VND)')}
        />
      </div>
      <div className="mt-space-sm border-t border-surface-container">
        <TotalRow
          label={t('Total Fare')}
          caption={t('All fees & taxes included')}
          amountVnd={totals.totalVnd}
        />
      </div>

      {canContinue ? (
        <>
          <button
            type="button"
            onClick={onContinue}
            disabled={busy}
            className="group mt-space-sm flex w-full items-center justify-between rounded-xl bg-teal-flow px-space-lg py-space-md font-headline-sm text-lg font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary disabled:opacity-70"
          >
            <span>{busy ? t('Holding your seat…') : label}</span>
            <Icon
              name="arrow_forward"
              className="text-[20px] transition-transform group-hover:translate-x-1"
            />
          </button>
          {failure && (
            <p role="alert" className="mt-2 text-center text-xs text-coral-glow">
              {failure.message ||
                t('We couldn’t hold your seat just now. Please check your connection and try again.')}
            </p>
          )}
        </>
      ) : (
        <>
          <button
            type="button"
            disabled
            className="mt-space-sm flex w-full cursor-not-allowed items-center justify-between rounded-xl bg-teal-flow px-space-lg py-space-md font-headline-sm text-lg font-semibold text-on-primary opacity-50"
          >
            <span>{label}</span>
            <Icon name="arrow_forward" className="text-[20px]" />
          </button>
          {!canBook && (
            <p role="status" className="mt-2 text-center text-xs text-on-surface-variant">
              {t('Ticket sales are closed for this trip.')}
            </p>
          )}
        </>
      )}
      <div className="mt-space-sm text-center text-xs">
        <Link to={changeTripTo} className="font-semibold text-teal-flow hover:underline">
          {t('Change Trip')}
        </Link>
      </div>
    </BookingCard>
  )
}
