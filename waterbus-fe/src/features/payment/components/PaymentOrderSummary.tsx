import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import {
  BookingCard,
  SeatFeeRow,
  SummaryRow,
  TotalRow,
} from '../../booking/components/SummaryParts'
import { seatLabel } from '../../booking/utils'
import type { BookingTotals } from '../../booking/utils'
import type { PassengerForm, Seat } from '../../booking/types'
import type { TripDetail } from '../../trips/types'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { addBooking } from '../../tickets/ticketsSlice'
import { bookedTicketId } from '../../tickets/utils'
import { useAppSelector } from '../../../store/hooks'
import { formatVnd, formatVndSuffix } from '../../../utils/format'
import { confirmPayment } from '../services/paymentService'
import { t } from '../../../i18n'

interface PaymentOrderSummaryProps {
  trip: TripDetail
  seat: Seat | undefined
  seatId: string
  passenger: PassengerForm
  totals: BookingTotals
}

export default function PaymentOrderSummary({
  trip,
  seat,
  seatId,
  passenger,
  totals,
}: PaymentOrderSummaryProps) {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const query = useAppSelector((s) => s.booking.query)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  // Mock: always succeeds, then moves on to the confirmation page.
  const complete = async () => {
    setBusy(true)
    setFailed(false)
    try {
      await confirmPayment()
      dispatch(
        addBooking({
          id: bookedTicketId(trip.id, seatId),
          tripId: trip.id,
          query,
          seat,
          seatId,
          passenger,
          totalVnd: totals.totalVnd,
        }),
      )
      navigate(ROUTES.bookingSuccess)
    } catch {
      setBusy(false)
      setFailed(true)
    }
  }

  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-24">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">{t('Order Summary')}</h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="rounded-xl bg-mist/70 p-space-md">
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
        <div className="my-2 h-px bg-outline-variant/60" />
        <div className="flex items-center justify-between text-[11px] text-on-surface-variant">
          <span>{t('Direct Line')}</span>
          <span>{t('{durationMins} min crossing', { durationMins: trip.durationMins })}</span>
          <span>{t('Non-stop')}</span>
        </div>
      </div>

      <div className="mt-space-md space-y-2">
        <div className="flex items-center justify-between text-sm">
          <span className="text-on-surface-variant">{t('Selected Seat')}</span>
          <span className="rounded bg-surface-container px-2 py-0.5 text-xs font-semibold text-deep-river">
            {seatLabel(seat, seatId)}
          </span>
        </div>
        <SummaryRow
          label={t('Passenger')}
          value={t('1 Passenger ({name})', { name: passenger.fullName })}
        />
        <SummaryRow label={t('Date')} value={`${trip.dateLabel}, 2025`} />
      </div>

      <div className="mt-space-md space-y-1.5 border-t border-surface-container pt-space-md">
        <SummaryRow label={t('Trip Fare')} value={formatVnd(totals.fareVnd)} />
        <SeatFeeRow
          label={t('Seat Fee')}
          seatFeeVnd={totals.seatFeeVnd}
          includedText={t('Included')}
        />
        <SummaryRow
          label={t('Discount')}
          value={`${totals.discountVnd > 0 ? '−' : ''}${formatVndSuffix(totals.discountVnd)}`}
          accent={totals.discountVnd > 0}
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
        onClick={complete}
        disabled={busy}
        className="group mt-space-sm flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary disabled:opacity-70"
      >
        {busy ? t('Confirming…') : t('I’ve Completed Payment')}
        <Icon
          name="arrow_forward"
          className="text-[18px] transition-transform group-hover:translate-x-1"
        />
      </button>
      {failed && (
        <p role="alert" className="mt-2 text-center text-xs text-coral-glow">
          {t('We couldn’t confirm your payment. Nothing was charged, please try again.')}
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
      <div className="mt-space-md flex items-start gap-2 border-t border-surface-container pt-space-md text-xs text-on-surface-variant">
        <Icon name="lock" className="text-[16px] text-teal-flow" />
        {t('Your selected seat remains reserved while you complete checkout.')}
      </div>
    </BookingCard>
  )
}
