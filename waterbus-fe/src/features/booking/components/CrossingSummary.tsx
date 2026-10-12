import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import type { BookingTotals } from '../utils'
import { isVip, seatLabel } from '../utils'
import { BookingCard, SeatFeeRow, SummaryRow, TotalRow } from './SummaryParts'
import { t } from '../../../i18n'

interface CrossingSummaryProps {
  trip: TripDetail
  seat: Seat | undefined
  seatId: string
  totals: BookingTotals
}

/** Sticky sidebar on passenger details: timeline, vessel facts and fare lines. */
export default function CrossingSummary({ trip, seat, seatId, totals }: CrossingSummaryProps) {
  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-24">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">
          {t('Crossing Summary')}
        </h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="rounded-xl bg-mist/70 p-space-md">
        <div className="mb-3 flex items-center justify-between text-xs">
          <span className="font-bold uppercase tracking-wider text-teal-flow">
            {trip.lineLabel}
          </span>
          <span className="text-on-surface-variant">
            {trip.dateLabel.replace(', ', ' • ')}, 2025
          </span>
        </div>
        <div className="flex gap-3">
          <div className="flex flex-col items-center">
            <span className="h-3 w-3 rounded-full bg-teal-flow" />
            <span className="my-1 w-0.5 flex-1 bg-teal-flow/40" />
            <span className="h-3 w-3 rounded-full bg-coral-glow" />
          </div>
          <div className="flex-1 space-y-space-md text-sm">
            <div className="flex justify-between">
              <div>
                <div className="font-semibold text-deep-river">{trip.originPierLabel}</div>
                <div className="text-xs text-on-surface-variant">
                  {t('{originGate} • Pontoon Terminal', { originGate: trip.originGate })}
                </div>
              </div>
              <span className="font-semibold text-deep-river">{trip.departTime}</span>
            </div>
            <span className="inline-flex items-center gap-1 rounded-full bg-white px-2.5 py-1 text-xs text-teal-flow shadow-sm">
              <Icon name="directions_boat" className="text-[14px]" />
              {t('{durationMins} min river run', { durationMins: trip.durationMins })}
            </span>
            <div className="flex justify-between">
              <div>
                <div className="font-semibold text-deep-river">{trip.destinationPierLabel}</div>
                <div className="text-xs text-on-surface-variant">
                  {t('{destinationGate} • Park Promenade', {
                    destinationGate: trip.destinationGate,
                  })}
                </div>
              </div>
              <span className="font-semibold text-deep-river">{trip.arriveTime}</span>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-space-md space-y-2 rounded-xl bg-sand-light/60 p-space-md">
        <SummaryRow label={t('Vessel')} value={`${trip.vesselCode} ${trip.vesselName}`} />
        <SummaryRow label={t('Selected Seat')} value={seatLabel(seat, seatId)} accent />
        <SummaryRow
          label={t('Cabin Class')}
          value={isVip(seat) ? t('VIP Lounge • Air-Conditioned') : t('Main Deck • Air-Conditioned')}
        />
        <SummaryRow label={t('Passengers')} value={t('1 Passenger')} />
      </div>

      <div className="mt-space-md space-y-1.5">
        <SummaryRow label={t('Standard River Fare')} value={formatVnd(totals.fareVnd)} />
        <SeatFeeRow
          label={t('Seat Reservation (Seat {id})', { id: seatId })}
          seatFeeVnd={totals.seatFeeVnd}
          includedText={t('Included (0 VND)')}
        />
        <SummaryRow label={t('Pier Dues & VAT')} value={t('Included')} accent />
      </div>
      <div className="mt-space-sm border-t border-surface-container">
        <TotalRow
          label={t('Total Amount')}
          caption={t('All taxes & fees included')}
          amountVnd={totals.totalVnd}
        />
      </div>

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
      <div className="mt-space-sm text-center">
        <Link
          to={ROUTES.seatSelection}
          className="text-sm font-semibold text-teal-flow hover:underline"
        >
          {t('Change Seat')}
        </Link>
      </div>
      <p className="mt-space-md border-t border-surface-container pt-space-md text-center text-xs text-on-surface-variant">
        {t('Your selected seat remains reserved while you complete checkout.')}
      </p>
    </BookingCard>
  )
}
