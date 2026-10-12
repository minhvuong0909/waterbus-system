import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { CATEGORY_LABEL } from '../../../mocks/booking'
import { ROUTES } from '../../../routes/routes'
import { formatNumber, formatVnd, formatVndSuffix } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import { validateVoucher } from '../services/bookingService'
import type { PassengerForm, Seat, Voucher } from '../types'
import type { BookingTotals } from '../utils'
import { seatTags } from '../utils'
import { BookingCard, CardHeading, SeatFeeRow, SummaryRow } from './SummaryParts'
import { t } from '../../../i18n'

function EditLink({
  to,
  icon = 'edit',
  children,
}: {
  to: string
  icon?: string
  children: string
}) {
  return (
    <Link
      to={to}
      className="flex items-center gap-1 whitespace-nowrap text-xs font-semibold text-teal-flow hover:underline"
    >
      <Icon name={icon} className="text-[14px]" />
      {children}
    </Link>
  )
}

function Fact({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="rounded-xl bg-mist/70 px-space-md py-space-sm">
      <div className="text-[11px] text-on-surface-variant">{label}</div>
      <div className={`text-sm font-semibold ${accent ? 'text-teal-flow' : 'text-deep-river'}`}>
        {value}
      </div>
    </div>
  )
}

export function JourneyDetailsCard({
  trip,
  seat,
  seatId,
}: {
  trip: TripDetail
  seat: Seat | undefined
  seatId: string
}) {
  return (
    <BookingCard>
      <CardHeading
        icon="directions_boat"
        title={t('Journey Details')}
        subtitle={t('Saigon River {line} • {code}', {
          line: trip.lineLabel,
          code: trip.vesselCode,
        })}
        aside={<EditLink to={ROUTES.seatSelection}>{t('Change Trip / Seat')}</EditLink>}
      />
      <div className="grid grid-cols-1 items-center gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-12">
        <div className="flex items-start gap-3 md:col-span-4">
          <span className="mt-1 flex h-5 w-5 items-center justify-center rounded-full bg-deep-river text-[10px] font-bold text-white">
            1
          </span>
          <div>
            <div className="font-headline-sm text-2xl font-bold leading-none text-deep-river">
              {trip.departTime}
            </div>
            <div className="mt-1 text-sm font-semibold text-deep-river">{trip.originPierLabel}</div>
            <div className="text-xs text-on-surface-variant">
              {t('{originGate} • Pontoon Terminal', { originGate: trip.originGate })}
            </div>
          </div>
        </div>
        <div className="flex flex-col items-center md:col-span-4">
          <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-0.5 text-xs font-semibold text-on-secondary-container">
            <Icon name="waves" className="text-[14px]" />
            {t('{durationMins} min river crossing', { durationMins: trip.durationMins })}
          </span>
          <div className="my-1 flex w-full items-center gap-2 text-outline">
            <span className="h-px flex-1 bg-outline-variant" />
            <Icon name="arrow_forward" className="text-[16px] text-teal-flow" />
            <span className="h-px flex-1 bg-outline-variant" />
          </div>
          <span className="text-xs text-on-surface-variant">{t('Non-stop express run')}</span>
        </div>
        <div className="flex items-start justify-end gap-3 text-right md:col-span-4">
          <div>
            <div className="font-headline-sm text-2xl font-bold leading-none text-deep-river">
              {trip.arriveTime}
            </div>
            <div className="mt-1 text-sm font-semibold text-deep-river">
              {trip.destinationPierLabel}
            </div>
            <div className="text-xs text-on-surface-variant">
              {t('{destinationGate} • Park Promenade', { destinationGate: trip.destinationGate })}
            </div>
          </div>
          <span className="mt-1 flex h-5 w-5 items-center justify-center rounded-full bg-teal-flow text-[10px] font-bold text-white">
            2
          </span>
        </div>
      </div>
      <div className="mt-space-sm grid grid-cols-1 gap-space-sm md:grid-cols-2">
        <Fact label={t('Vessel')} value={trip.vesselCode} />
        <Fact
          label={t('Selected Seat')}
          value={seatTags(seat) ? `${seatId} (${seatTags(seat)})` : seatId}
          accent
        />
        <Fact label={t('Date')} value={`${trip.dateLabel}, 2025`} />
      </div>
    </BookingCard>
  )
}

export function PassengerInfoCard({ passenger }: { passenger: PassengerForm }) {
  const dob = passenger.dateOfBirth.replace(/\s/g, '')
  return (
    <BookingCard>
      <CardHeading
        icon="badge"
        title={t('Passenger Information')}
        subtitle={t('Passenger 1 (Primary Traveler)')}
        aside={<EditLink to={ROUTES.passengerDetails}>{t('Edit Passenger Details')}</EditLink>}
      />
      <div className="grid grid-cols-1 gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-2">
        <div>
          <div className="text-xs text-on-surface-variant">{t('Full Legal Name')}</div>
          <div className="text-sm font-semibold text-deep-river">{passenger.fullName}</div>
        </div>
        <div>
          <div className="text-xs text-on-surface-variant">{t('Passenger Category')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {t('{category} • Born {dob}', { category: t(CATEGORY_LABEL[passenger.category]), dob })}
          </div>
        </div>
        <div>
          <div className="text-xs text-on-surface-variant">{t('Contact Mobile')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {passenger.phoneCode} {passenger.phone}
          </div>
        </div>
        <div>
          <div className="text-xs text-on-surface-variant">{t('Notification Email')}</div>
          <div className="text-sm font-semibold text-deep-river">{passenger.email}</div>
        </div>
      </div>
    </BookingCard>
  )
}

export function ETicketContactCard({ passenger }: { passenger: PassengerForm }) {
  return (
    <BookingCard>
      <CardHeading
        icon="qr_code_2"
        title={t('E-Ticket & Booking Contact')}
        subtitle={t('Your QR ticket will be available after successful payment.')}
      />
      <p className="text-xs text-on-surface-variant">
        {t('Sent to')} <span className="text-deep-river">{passenger.email}</span> {t('and')}{' '}
        <span className="text-deep-river">
          {passenger.phoneCode} {passenger.phone}
        </span>
        .
      </p>
    </BookingCard>
  )
}

export function VoucherCard({
  voucher,
  onApply,
}: {
  voucher: Voucher | null
  onApply: (v: Voucher | null) => void
}) {
  const [code, setCode] = useState(voucher?.code ?? '')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)

  const apply = async () => {
    setBusy(true)
    try {
      const result = await validateVoucher(code)
      onApply(result)
      setMessage(
        result
          ? t('Voucher applied: {percent}% off your fare.', { percent: result.percentOff })
          : t('This code is not valid.'),
      )
      setFailed(false)
    } catch {
      setMessage(t('We couldn’t check this code. Please try again.'))
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <BookingCard className="py-space-md">
      <div className="flex flex-col items-start gap-space-md md:flex-row md:items-center">
        <span className="flex items-center gap-2 text-sm font-semibold text-deep-river">
          <Icon name="confirmation_number" className="text-[20px] text-teal-flow" />
          {t('Voucher / Corporate Discount')}
        </span>
        <input
          value={code}
          onChange={(e) => setCode(e.target.value)}
          placeholder={t('Enter voucher or promo code')}
          aria-label={t('Voucher or promo code')}
          className="flex-1 rounded-xl border-0 bg-mist px-space-md py-2.5 text-sm text-deep-river placeholder:text-outline focus:ring-2 focus:ring-teal-flow/30"
        />
        <button
          type="button"
          disabled={busy}
          onClick={apply}
          className="rounded-xl bg-surface-container px-space-lg py-2.5 text-sm font-semibold text-deep-river transition-colors hover:bg-surface-container-high disabled:opacity-60"
        >
          {t('Apply')}
        </button>
      </div>
      {message && (
        <p
          role={failed ? 'alert' : 'status'}
          className={`mt-2 text-xs ${
            failed ? 'text-coral-glow' : voucher ? 'text-teal-flow' : 'text-on-surface-variant'
          }`}
        >
          {message}
        </p>
      )}
    </BookingCard>
  )
}

export function TermsConfirm({
  checked,
  onChange,
}: {
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-2xl bg-surface-container px-space-lg py-space-md text-xs text-on-surface-variant">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-5 w-5 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow"
      />
      <span>
        {t('I confirm that the booking details above are correct. I accept the')}{' '}
        <span className="text-teal-flow underline">{t('Waterway Carriage Rules')}</span>.
      </span>
    </label>
  )
}

export function OrderSummary({
  trip,
  totals,
  canContinue,
}: {
  trip: TripDetail
  totals: BookingTotals
  /** False until the passenger accepts the carriage rules. */
  canContinue: boolean
}) {
  return (
    <BookingCard className="p-space-lg lg:sticky lg:top-24">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="font-headline-md text-headline-sm text-deep-river">{t('Order Summary')}</h2>
        <span className="rounded-full bg-secondary-container px-2.5 py-0.5 text-[11px] font-bold text-teal-flow">
          {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="rounded-xl bg-mist/70 p-space-md">
        <div className="mb-2 flex items-center justify-between text-xs font-semibold text-deep-river">
          <span>{trip.originPierLabel}</span>
          <Icon name="sailing" className="text-[16px] text-teal-flow" />
          <span>{trip.destinationPierLabel}</span>
        </div>
        <div className="h-1 overflow-hidden rounded-full bg-surface-container">
          <div className="h-full w-3/4 rounded-full bg-teal-flow" />
        </div>
        <div className="mt-2 flex items-center justify-between text-[11px] text-on-surface-variant">
          <span>{t('Depart {departTime}', { departTime: trip.departTime })}</span>
          <span>{t('Direct Line')}</span>
          <span>{t('Arrive {arriveTime}', { arriveTime: trip.arriveTime })}</span>
        </div>
      </div>

      <div className="mt-space-md space-y-1.5">
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

      <div className="mt-space-md flex items-center justify-between rounded-xl bg-mist/70 px-space-md py-space-md">
        <span className="font-headline-sm text-lg font-bold text-deep-river">{t('Total')}</span>
        <span className="text-right">
          <span className="block font-numeric-lg text-numeric-lg font-bold leading-none text-deep-river">
            {formatNumber(totals.totalVnd)}
          </span>
          <span className="text-xs font-semibold text-teal-flow">{t('VND')}</span>
        </span>
      </div>

      {canContinue ? (
        <Link
          to={ROUTES.payment}
          className="group mt-space-md flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary"
        >
          {t('Continue to Payment')}
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
            className="mt-space-md flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-headline-sm text-base font-semibold text-on-primary opacity-50"
          >
            {t('Continue to Payment')}
            <Icon name="arrow_forward" className="text-[18px]" />
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
      <div className="mt-space-md flex items-start gap-2 text-xs text-on-surface-variant">
        <Icon name="verified_user" className="text-[18px] text-teal-flow" />
        {t('Your selected seat remains reserved while you complete checkout.')}
      </div>
    </BookingCard>
  )
}
