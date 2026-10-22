import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import {
  BookingCard,
  SeatFeeRow,
  SummaryRow,
  TotalRow,
} from '../../booking/components/SummaryParts'
import { selectedSeatsLabel } from '../../booking/utils'
import type { BookingTotals } from '../../booking/utils'
import type { PurchaserForm, Seat } from '../../booking/types'
import type { TripDetail } from '../../trips/types'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { holdExtended } from '../../booking/bookingSlice'
import { useHoldCountdown } from '../../booking/hooks/useHoldCountdown'
import { formatVnd } from '../../../utils/format'
import { createPaymentUrl } from '../services/paymentService'
import { t } from '../../../i18n'
import SummaryTimer from '../../booking/components/SummaryTimer'

interface PaymentOrderSummaryProps {
  trip: TripDetail
  seats: Seat[]
  purchaser: PurchaserForm
  /** Covers both legs for a round trip. */
  totals: BookingTotals
  /** The return leg of a round trip, paid in the same order (BR-COM-02). */
  returnLeg?: { trip: TripDetail; seats: Seat[] }
}

/** Piers, times and day of one leg. */
function LegRoute({ trip, label }: { trip: TripDetail; label?: string }) {
  return (
    <div className="rounded-xl bg-mist/70 p-space-md">
      {label && <div className="mb-1 text-[11px] font-bold uppercase tracking-wider text-teal-flow">{label}</div>}
      <div className="flex items-start justify-between text-xs font-semibold text-deep-river">
        <span>
          {trip.originPierLabel}
          <span className="block font-normal text-teal-flow">{trip.departTime}</span>
        </span>
        <Icon name="sailing" className="text-[18px] text-teal-flow" />
        <span className="text-right">
          {trip.destinationPierLabel}
          <span className="block font-normal text-teal-flow">{trip.arriveTime}</span>
        </span>
      </div>
      <div className="mt-2 border-t border-outline-variant/60 pt-2 text-center text-[11px] text-on-surface-variant">
        {trip.dateFull} • {t('{durationMins} min crossing', { durationMins: trip.durationMins })}
      </div>
    </div>
  )
}

export default function PaymentOrderSummary({
  trip,
  seats,
  purchaser,
  totals,
  returnLeg,
}: PaymentOrderSummaryProps) {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const order = useAppSelector((s) => s.booking.order)
  const { hold, expired } = useHoldCountdown()
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  // BR-PAY-01: paying only sends the traveler to the gateway. The ticket is issued when the
  // backend verifies the payment, and the callback page waits for that; there is no "I have paid".
  const pay = async () => {
    if (!hold) return
    setBusy(true)
    setFailed(false)
    try {
      const { url, expiresAt } = await createPaymentUrl(hold.orderId, order?.accessToken ?? hold.accessToken)
      if (expiresAt !== null) dispatch(holdExtended(expiresAt))
      if (url.startsWith('/')) navigate(url)
      else window.location.assign(url)
    } catch {
      setBusy(false)
      setFailed(true)
    }
  }

  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-[5.5rem] lg:max-h-[calc(100svh-6.5rem)] lg:overflow-y-auto lg:overscroll-contain">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">{t('Order Summary')}</h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <SummaryTimer />

      <div className="space-y-2">
        <LegRoute trip={trip} label={returnLeg ? t('Outbound trip') : undefined} />
        {returnLeg && <LegRoute trip={returnLeg.trip} label={t('Return trip')} />}
      </div>

      <div className="mt-space-md space-y-2">
        <div className="flex items-center justify-between text-sm">
          <span className="text-on-surface-variant">
            {returnLeg ? t('Outbound seats') : seats.length > 1 ? t('Selected Seats') : t('Selected Seat')}
          </span>
          <span className="rounded bg-surface-container px-2 py-0.5 text-xs font-semibold text-deep-river">
            {selectedSeatsLabel(seats)}
          </span>
        </div>
        {returnLeg && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-on-surface-variant">{t('Return seats')}</span>
            <span className="rounded bg-surface-container px-2 py-0.5 text-xs font-semibold text-deep-river">
              {selectedSeatsLabel(returnLeg.seats)}
            </span>
          </div>
        )}
        <SummaryRow label={t('Buyer')} value={purchaser.fullName} />
      </div>

      <div className="mt-space-sm space-y-1.5 border-t border-surface-container pt-space-sm">
        <SummaryRow label={t('Trip Fare')} value={formatVnd(totals.fareVnd)} />
        <SeatFeeRow
          label={t('Seat Fee')}
          seatFeeVnd={totals.seatFeeVnd}
          includedText={t('Included')}
        />
      </div>
      <div className="mt-space-sm border-t border-surface-container pt-space-sm">
        <TotalRow
          label={t('Total Amount')}
          caption={t('All taxes & fees included')}
          amountVnd={totals.totalVnd}
        />
      </div>

      <button
        type="button"
        onClick={pay}
        disabled={busy || expired || !hold}
        className="group mt-space-sm flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary disabled:opacity-70"
      >
        {busy ? t('Opening the payment page…') : t('Pay with VNPay')}
        <Icon
          name="arrow_forward"
          className="text-[18px] transition-transform group-hover:translate-x-1"
        />
      </button>
      <p className="mt-2 text-center text-xs text-on-surface-variant">
        {t('You will be redirected to VNPay. Your tickets are issued once the payment is confirmed.')}
      </p>
      {failed && (
        <p role="alert" className="mt-2 text-center text-xs text-coral-glow">
          {t('We couldn’t open the payment page. Nothing was charged, please try again.')}
        </p>
      )}
      <div className="mt-space-sm text-center">
        <Link
          to={ROUTES.checkoutReview}
          className="text-xs text-on-surface-variant hover:text-deep-river"
        >
          {t('← Back to Checkout')}
        </Link>
      </div>
    </BookingCard>
  )
}
