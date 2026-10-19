import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd, formatVndSuffix } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import type { BookingFailure } from '../hooks/useCheckout'
import type { BookingTotals } from '../utils'
import { MAX_SEATS_PER_BOOKING } from '../utils'
import { cabinsLabel, hasSurcharge, passengersLabel, seatFeeFor, seatPosition, seatZone } from '../utils'
import { BookingCard, SeatFeeRow, SummaryRow, TotalRow } from './SummaryParts'
import { t } from '../../../i18n'

/** White strip under the page title: vessel, times, duration, fare and "Change Trip". */
export function TripStrip({ trip, seats, totalVnd }: { trip: TripDetail; seats: Seat[]; totalVnd: number }) {
  return (
    <div className="flex flex-col items-start justify-between gap-space-md rounded-2xl bg-white p-space-md shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:flex-row md:items-center">
      <div className="flex flex-wrap items-center gap-space-md">
        <div className="flex items-center gap-2 rounded-xl bg-deep-river px-space-md py-2 text-on-primary">
          <Icon name="directions_boat" className="text-[20px]" />
          <span className="font-headline-sm text-lg font-bold">{trip.vesselCode}</span>
        </div>
        <div className="flex items-center gap-space-md">
          <div>
            <div className="font-headline-sm text-lg font-bold text-deep-river">
              {trip.departTime}
            </div>
            <div className="text-[11px] text-on-surface-variant">{trip.originPierLabel}</div>
          </div>
          <Icon name="arrow_forward" className="text-[18px] text-outline" />
          <div>
            <div className="font-headline-sm text-lg font-bold text-deep-river">
              {trip.arriveTime}
            </div>
            <div className="text-[11px] text-on-surface-variant">{trip.destinationPierLabel}</div>
          </div>
        </div>
        <span className="flex items-center gap-1 rounded-full bg-mist px-3 py-1.5 text-xs font-medium text-on-surface-variant">
          <Icon name="schedule" className="text-[15px] text-teal-flow" />
          {t('{durationMins} min {crossingKind} Crossing', {
            durationMins: trip.durationMins,
            crossingKind: trip.crossingKind,
          })}
        </span>
      </div>
      <div className="flex items-center gap-space-lg">
        <div className="text-right">
          <div className="text-[11px] text-on-surface-variant">
            {t('{passengers} • {class}', {
              passengers: passengersLabel(seats.length),
              class: cabinsLabel(seats),
            })}
          </div>
          <div className="font-headline-sm text-lg font-bold text-deep-river">
            {formatVndSuffix(totalVnd)}
          </div>
        </div>
        <Link
          to={ROUTES.searchResults}
          className="flex items-center gap-1 text-sm font-semibold text-teal-flow hover:text-deep-river"
        >
          {t('Change Trip')} <Icon name="edit" className="text-[16px]" />
        </Link>
      </div>
    </div>
  )
}

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
}

export function BookingSummary({
  trip,
  seats,
  totals,
  limitReached,
  canBook,
  onContinue,
  busy,
  failure,
}: BookingSummaryProps) {
  const surcharge = seats.some(hasSurcharge)
  // An empty selection, or one with a seat somebody else took, must not reach checkout.
  const canContinue =
    canBook && seats.length > 0 && seats.every((seat) => seat.status === 'available')
  return (
    <div className="space-y-space-md lg:sticky lg:top-24 lg:max-h-[calc(100svh-7rem)] lg:overflow-y-auto lg:overscroll-contain lg:-m-1 lg:p-1">
      <BookingCard className="p-space-lg">
        <div className="mb-space-md flex items-center justify-between">
          <h2 className="font-headline-md text-headline-sm text-deep-river">
            {t('Booking Summary')}
          </h2>
          <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
            {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
          </span>
        </div>

        <div className="space-y-2 rounded-xl border border-outline-variant/40 bg-mist/60 p-space-md">
          <SummaryRow label={t('Route & Vessel')} value={trip.vesselName} />
          <SummaryRow
            label={t('Date & Time')}
            value={t('{date}, {time} Departure', { date: trip.dateLabel, time: trip.departTime })}
          />
          <div className="flex items-center justify-between gap-2 pt-1 text-xs text-deep-river">
            <span className="flex items-center gap-1.5 font-medium">
              <Icon name="directions_boat" className="text-[16px] text-teal-flow" />
              {trip.originPierName} ➔ {trip.destinationPierName}
            </span>
            <span className="rounded border border-outline-variant/40 bg-white px-2 py-1 text-[11px] text-teal-flow">
              {t('{vesselCode} {crossingKind} ({durationMins} min)', {
                vesselCode: trip.vesselCode,
                crossingKind: trip.crossingKind,
                durationMins: trip.durationMins,
              })}
            </span>
          </div>
        </div>

        <div className="mt-space-md rounded-xl border border-teal-flow/25 bg-sand-light/60 p-space-md">
          <div className="flex items-center justify-between text-[10px] font-bold uppercase tracking-wider text-teal-flow">
            {t('Reserved Seat Choice')}
            <Icon name="airline_seat_recline_extra" className="text-[16px]" />
          </div>
          {seats.length === 0 ? (
            <p className="mt-2 text-xs text-on-surface-variant">
              {t('No seat chosen yet. Tap a seat on the map.')}
            </p>
          ) : (
            <ul className="mt-1 divide-y divide-outline-variant/30">
              {seats.map((seat) => {
                const priced = seat.priceVnd !== undefined
                const fee = priced ? (seat.priceVnd ?? 0) : seatFeeFor(seat, trip.fareVnd)
                return (
                  <li key={seat.id} className="flex items-center justify-between gap-2 py-1.5">
                    <span className="font-headline-sm text-xl font-bold text-deep-river">
                      {seat.code}
                    </span>
                    <span className="flex-1 text-right text-xs text-on-surface-variant">
                      {seatPosition(seat)} • {seatZone(seat)}
                    </span>
                    <span className={`w-20 text-right text-xs ${fee > 0 ? 'font-semibold text-deep-river' : 'text-on-surface-variant'}`}>
                      {priced
                        ? formatVndSuffix(fee)
                        : fee > 0
                          ? `+${formatVndSuffix(fee)}`
                          : t('Included')}
                    </span>
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
                ? t('Standard Transit Fare × {count}', { count: totals.seatCount })
                : t('Standard Transit Fare')
            }
            value={formatVnd(totals.fareVnd)}
          />
          <SeatFeeRow
            label={surcharge ? t('Cabin Surcharge') : t('Seat Reservation Fee')}
            seatFeeVnd={totals.seatFeeVnd}
            includedText={t('Included (0 VND)')}
          />
          <SummaryRow label={t('Harbor Fees & VAT')} value={t('Included')} accent />
        </div>
        <div className="bg-white pb-1 lg:sticky lg:bottom-0">
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
              <span>{busy ? t('Holding your seat…') : t('Continue to Passenger Details')}</span>
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
              <span>{t('Continue to Passenger Details')}</span>
              <Icon name="arrow_forward" className="text-[20px]" />
            </button>
            <p role="status" className="mt-2 text-center text-xs text-on-surface-variant">
              {canBook
                ? t('Please choose an available seat to continue.')
                : t('Ticket sales are closed for this trip.')}
            </p>
          </>
        )}
        </div>
        <div className="mt-space-sm flex items-center justify-center gap-2 text-xs text-teal-flow">
          <Link to={ROUTES.searchResults} className="hover:underline">
            {t('Change Trip')}
          </Link>
          <span className="text-outline">•</span>
          <button type="button" className="hover:underline">
            {t('Fare Policies')}
          </button>
        </div>
        <div className="mt-space-md flex items-start gap-2 rounded-lg bg-mist px-space-sm py-space-sm text-xs text-on-surface-variant">
          <Icon name="timer" className="text-[16px] text-teal-flow" />
          {t(
            'Seats held for 10:00 during checkout. Instant digital QR boarding pass delivered immediately.',
          )}
        </div>
      </BookingCard>

      <div className="flex items-center gap-space-md rounded-2xl bg-white p-space-md shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-sand-light text-teal-flow">
          <Icon name="support_agent" className="text-[22px]" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-sm font-semibold text-deep-river">
            {t('{originPierName} Concierge', { originPierName: trip.originPierName })}
          </div>
          <div className="text-xs text-on-surface-variant">
            {t('Daily 06:00 - 22:00 Live Assistance')}
          </div>
        </div>
        <button type="button" className="text-sm font-semibold text-teal-flow hover:underline">
          {t('Contact')}
        </button>
      </div>
    </div>
  )
}
