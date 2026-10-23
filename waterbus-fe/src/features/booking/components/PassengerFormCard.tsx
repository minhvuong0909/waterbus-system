import type { ReactNode } from 'react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { PurchaserForm, Seat, SeatPassengerForm } from '../types'
import { purchaserErrors, seatPassengerErrors, seatTags } from '../utils'
import { BookingCard } from './SummaryParts'
import { t } from '../../../i18n'

interface PassengerFormCardProps {
  purchaser: PurchaserForm
  /** The chosen seats; each one needs its own passenger (BR-COM-06). */
  seats: Seat[]
  /** Passenger of each seat, by seat id. */
  passengers: Record<string, SeatPassengerForm>
  onPurchaser: (patch: Partial<PurchaserForm>) => void
  onPassenger: (seatId: string, patch: Partial<SeatPassengerForm>) => void
  /** False hides the buyer card and the footer, e.g. for the first leg of a round trip. */
  showBuyer?: boolean
  /** Shown above the passengers, e.g. which leg of a round trip they travel on. */
  heading?: ReactNode
}

const FIELD =
  'w-full rounded-xl border-0 bg-mist py-3 text-sm text-deep-river placeholder:text-outline focus:ring-2 focus:ring-teal-flow/30'

function Field({
  label,
  hint,
  icon,
  error,
  children,
}: {
  label: string
  hint?: string
  icon?: string
  /** Message shown under the field; empty when the value is fine or not checked yet. */
  error?: string
  children: React.ReactNode
}) {
  return (
    <label className="block">
      <span className="mb-1.5 flex items-center justify-between text-xs font-semibold text-on-surface">
        {label}
        {hint && <span className="font-normal text-on-surface-variant">{hint}</span>}
      </span>
      <span className="relative block">
        {icon && (
          <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-outline">
            <Icon name={icon} className="text-[18px]" />
          </span>
        )}
        {children}
      </span>
      {error && (
        <span role="alert" className="mt-1 block text-xs text-coral-glow">
          {error}
        </span>
      )}
    </label>
  )
}

/** Text input that reports a problem only after the traveler has left the field once. */
function TextField({
  label,
  hint,
  icon,
  value,
  error,
  onChange,
  type = 'text',
  autoComplete,
  required,
}: {
  label: string
  hint?: string
  icon?: string
  value: string
  error?: string
  onChange: (value: string) => void
  type?: string
  autoComplete?: string
  required?: boolean
}) {
  const [touched, setTouched] = useState(false)
  return (
    <Field label={label} hint={hint} icon={icon} error={touched ? error : undefined}>
      <input
        className={`${FIELD} ${icon ? 'pl-10' : 'px-3'}`}
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={() => setTouched(true)}
        autoComplete={autoComplete}
        required={required}
        aria-invalid={touched && error ? true : undefined}
      />
    </Field>
  )
}

function SectionTitle({ icon, title, subtitle }: { icon: string; title: string; subtitle: string }) {
  return (
    <div className="mb-space-md flex items-center gap-space-sm">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-sand-light text-teal-flow">
        <Icon name={icon} className="text-[22px]" />
      </div>
      <div>
        <h2 className="font-headline-md text-headline-sm text-deep-river">{title}</h2>
        <p className="text-xs text-on-surface-variant">{subtitle}</p>
      </div>
    </div>
  )
}

export default function PassengerFormCard({
  purchaser,
  seats,
  passengers,
  onPurchaser,
  onPassenger,
  showBuyer = true,
  heading,
}: PassengerFormCardProps) {
  const buyerErrors = purchaserErrors(purchaser)

  return (
    <div className="space-y-space-md">
      {heading}
      {seats.map((seat, index) => {
        const passenger = passengers[seat.id]
        const errors = seatPassengerErrors(passenger)
        const tags = seatTags(seat)
        return (
          <BookingCard key={seat.id} className="p-space-lg md:p-space-xl">
            <div className="mb-space-md flex flex-wrap items-center justify-between gap-space-md">
              <SectionTitle
                icon="person"
                title={t('Passenger {number}', { number: index + 1 })}
                subtitle={t('Who will travel on this seat')}
              />
              <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
                <Icon name="airline_seat_recline_extra" className="text-[15px]" />
                {t('Seat {id}', { id: seat.code })}
                {tags && ` • ${tags}`}
              </span>
            </div>
            <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
              <TextField
                label={t('Full Name')}
                icon="badge"
                value={passenger?.fullName ?? ''}
                error={errors.fullName}
                onChange={(fullName) => onPassenger(seat.id, { fullName })}
                autoComplete="off"
                required
              />
              <TextField
                label={t('Email Address')}
                hint={t('Optional')}
                icon="mail"
                type="email"
                value={passenger?.email ?? ''}
                error={errors.email}
                onChange={(email) => onPassenger(seat.id, { email })}
              />
              <TextField
                label={t('Phone Number')}
                hint={t('Optional')}
                value={passenger?.phone ?? ''}
                onChange={(phone) => onPassenger(seat.id, { phone })}
                autoComplete="off"
              />
            </div>
            <button
              type="button"
              onClick={() =>
                onPassenger(seat.id, {
                  fullName: purchaser.fullName,
                  email: purchaser.email,
                  phone: purchaser.phone,
                })
              }
              className="mt-space-sm text-xs font-semibold text-teal-flow hover:underline"
            >
              {t('Same as the buyer')}
            </button>
          </BookingCard>
        )
      })}

      {showBuyer && (
        <>
          <BookingCard className="p-space-lg md:p-space-xl">
            <SectionTitle
              icon="contact_mail"
              title={t('Buyer')}
              subtitle={t('The person who pays for this order')}
            />
            <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
              <TextField
                label={t('Full Name')}
                icon="badge"
                value={purchaser.fullName}
                error={buyerErrors.fullName}
                onChange={(fullName) => onPurchaser({ fullName })}
                autoComplete="name"
                required
              />
              <TextField
                label={t('Email Address')}
                icon="mail"
                type="email"
                value={purchaser.email}
                error={buyerErrors.email}
                onChange={(email) => onPurchaser({ email })}
                autoComplete="email"
                required
              />
              <Field label={t('Phone Number')} hint={t('Optional')} error={undefined}>
                <span className="flex gap-2">
                  <span className="flex items-center rounded-xl bg-mist px-3 text-sm text-deep-river">
                    <span className="mr-1 text-xs text-on-surface-variant">{t('VN')}</span>
                    {purchaser.phoneCode}
                  </span>
                  <input
                    className={`${FIELD} px-3`}
                    value={purchaser.phone}
                    onChange={(e) => onPurchaser({ phone: e.target.value })}
                    autoComplete="tel-national"
                    aria-invalid={buyerErrors.phone ? true : undefined}
                  />
                </span>
              </Field>
            </div>
            {buyerErrors.phone && (
              <p role="alert" className="mt-1 text-xs text-coral-glow">
                {buyerErrors.phone}
              </p>
            )}
          </BookingCard>

          <div className="flex items-center justify-between gap-3 rounded-2xl bg-white px-space-lg py-space-md text-xs text-on-surface-variant shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
            <span className="flex items-center gap-2">
              <Icon name="info" className="text-[18px] text-teal-flow" />
              {t('Each passenger gets a ticket in their own name.')}
            </span>
            <Link
              to={ROUTES.seatSelection}
              className="whitespace-nowrap font-semibold text-teal-flow hover:underline"
            >
              {t('Return to seat map')}
            </Link>
          </div>
        </>
      )}
    </div>
  )
}
