import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatNumber, formatVnd } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { BookingFailure } from '../hooks/useCheckout'
import type { BookingTotals } from '../utils'
import BookingCard from './BookingCard'
import SummaryRow from './SummaryRow'
import { t } from '../../../i18n'
import SummaryTimer from './SummaryTimer'

export default function OrderSummary({
  trip,
  totals,
  canContinue,
  busy,
  failure,
  onContinue,
  returnTrip,
}: {
  trip: TripDetail
  /** The return trip of a round trip; `totals` then covers both legs. */
  returnTrip?: TripDetail
  totals: BookingTotals
  /** False until the passenger accepts the carriage rules. */
  canContinue: boolean
  /** True while the booking is being created. */
  busy: boolean
  failure: BookingFailure | null
  onContinue: () => void
}) {
  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-[5.5rem] lg:max-h-[calc(100svh-6.5rem)] lg:overflow-y-auto lg:overscroll-contain">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-display text-headline-sm text-deep-river">{t('Order Summary')}</h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-2xs font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <SummaryTimer />

      <div className="rounded-xl bg-mist/70 p-space-md">
        <div className="mb-2 flex items-center justify-between text-xs font-semibold text-deep-river">
          <span>{trip.originPierLabel}</span>
          <Icon name="sailing" className="text-base text-teal-flow" />
          <span>{trip.destinationPierLabel}</span>
        </div>
        <div className="h-1 overflow-hidden rounded-full bg-surface-container">
          <div className="h-full w-3/4 rounded-full bg-teal-flow" />
        </div>
        <div className="mt-2 flex items-center justify-between text-2xs text-on-surface-variant">
          <span>{t('Depart {departTime}', { departTime: trip.departTime })}</span>
          <span>{t('Direct Line')}</span>
          <span>{t('Arrive {arriveTime}', { arriveTime: trip.arriveTime })}</span>
        </div>
      </div>
      {returnTrip && (
        <div className="mt-2 rounded-xl bg-mist/70 p-space-md">
          <div className="mb-1 text-2xs font-bold uppercase tracking-wider text-teal-flow">{t('Return trip')}</div>
          <div className="flex items-center justify-between text-xs font-semibold text-deep-river">
            <span>{returnTrip.originPierLabel}</span>
            <Icon name="sailing" className="text-base text-teal-flow" />
            <span>{returnTrip.destinationPierLabel}</span>
          </div>
          <div className="mt-1 flex items-center justify-between text-2xs text-on-surface-variant">
            <span>{t('Depart {departTime}', { departTime: returnTrip.departTime })}</span>
            <span>{returnTrip.dateShort}</span>
            <span>{t('Arrive {arriveTime}', { arriveTime: returnTrip.arriveTime })}</span>
          </div>
        </div>
      )}

      <div className="mt-space-md space-y-1.5">
        <SummaryRow label={t('Trip Fare')} value={formatVnd(totals.totalVnd)} />
      </div>

      <div className="mt-space-md flex items-center justify-between rounded-xl bg-mist/70 px-space-md py-space-md">
        <span className="font-display text-lg font-bold text-deep-river">{t('Total')}</span>
        <span className="text-right">
          <span className="block font-display text-numeric-lg font-bold leading-none text-deep-river">
            {formatNumber(totals.totalVnd)}
          </span>
          <span className="text-xs font-semibold text-teal-flow">{t('VND')}</span>
        </span>
      </div>

      {canContinue ? (
        <>
          <button
            type="button"
            onClick={onContinue}
            disabled={busy}
            className="group mt-space-md flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-display text-base font-semibold text-on-primary shadow-glow transition-all hover:bg-secondary disabled:opacity-70"
          >
            {busy ? t('Preparing your order…') : t('Continue to Payment')}
            <Icon
              name="arrow_forward"
              className="text-lg transition-transform group-hover:translate-x-1"
            />
          </button>
          {failure && (
            <p role="alert" className="mt-2 text-center text-xs text-coral-glow">
              {failure.message ||
                t('We couldn’t prepare your order just now. Please check your connection and try again.')}{' '}
              {failure.seatTaken && (
                <Link to={ROUTES.seatSelection} className="font-semibold underline">
                  {t('Choose another seat')}
                </Link>
              )}
            </p>
          )}
        </>
      ) : (
        <>
          <button
            type="button"
            disabled
            className="mt-space-md flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-display text-base font-semibold text-on-primary opacity-50"
          >
            {t('Continue to Payment')}
            <Icon name="arrow_forward" className="text-lg" />
          </button>
          <p role="status" className="mt-2 text-center text-xs text-on-surface-variant">
            {t('Please accept the Waterway Carriage Rules to continue.')}
          </p>
        </>
      )}
      <div className="mt-space-sm text-center">
        <Link
          to={ROUTES.passengerDetails}
          className="text-xs text-on-surface-variant hover:text-deep-river"
        >
          {t('← Back to Passenger Details')}
        </Link>
      </div>
    </BookingCard>
  )
}
