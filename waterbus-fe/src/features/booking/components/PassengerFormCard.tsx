import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { PassengerForm } from '../types'
import { BookingCard } from './SummaryParts'
import { t } from '../../../i18n'

interface PassengerFormCardProps {
  passenger: PassengerForm
  /** Codes of the chosen seats, e.g. "S01, F03". */
  seatCodes: string
  seatCount: number
  onChange: (patch: Partial<PassengerForm>) => void
}

const FIELD =
  'w-full rounded-xl border-0 bg-mist py-3 text-sm text-deep-river placeholder:text-outline focus:ring-2 focus:ring-teal-flow/30'

function Field({
  label,
  hint,
  icon,
  children,
}: {
  label: string
  hint?: string
  icon?: string
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
    </label>
  )
}

export default function PassengerFormCard({
  passenger,
  seatCodes,
  seatCount,
  onChange,
}: PassengerFormCardProps) {
  return (
    <div className="space-y-space-md">
      <BookingCard className="p-space-lg md:p-space-xl">
        <div className="mb-space-lg flex flex-wrap items-center justify-between gap-space-md">
          <div className="flex items-center gap-space-sm">
            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-sand-light text-teal-flow">
              <Icon name="person" className="text-[24px]" />
            </div>
            <div>
              <h2 className="font-headline-md text-headline-sm text-deep-river">
                {t('Passenger 1 (Primary Traveler)')}
              </h2>
              <p className="text-xs text-on-surface-variant">{t('Primary traveler')}</p>
            </div>
          </div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
            <Icon name="airline_seat_recline_extra" className="text-[15px]" />
            {t(seatCount === 1 ? 'Seat {seatCodes}' : 'Seats {seatCodes}', { seatCodes })}
          </span>
        </div>
        {seatCount > 1 && (
          <p className="mb-space-md rounded-lg bg-sand-light/70 px-3 py-2 text-xs text-on-surface-variant">
            {t('One booking, one contact: all {count} tickets are issued in this name.', { count: seatCount })}
          </p>
        )}

        <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
          <Field label={t('Full Name')} icon="badge">
            <input
              className={`${FIELD} pl-10`}
              value={passenger.fullName}
              onChange={(e) => onChange({ fullName: e.target.value })}
              autoComplete="name"
            />
          </Field>
          <Field label={t('Phone Number')}>
            <span className="flex gap-2">
              <span className="flex items-center rounded-xl bg-mist px-3 text-sm text-deep-river">
                <span className="mr-1 text-xs text-on-surface-variant">{t('VN')}</span>
                {passenger.phoneCode}
              </span>
              <input
                className={`${FIELD} px-3`}
                value={passenger.phone}
                onChange={(e) => onChange({ phone: e.target.value })}
                autoComplete="tel-national"
              />
            </span>
          </Field>
          <Field label={t('Email Address')} icon="mail">
            <input
              className={`${FIELD} pl-10`}
              type="email"
              value={passenger.email}
              onChange={(e) => onChange({ email: e.target.value })}
              autoComplete="email"
            />
          </Field>
        </div>

        <div className="mt-space-lg border-t border-surface-container pt-space-lg">
          <div className="mb-space-sm flex items-center gap-space-sm">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-sand-light text-teal-flow">
              <Icon name="contact_mail" className="text-[20px]" />
            </div>
            <div>
              <h3 className="font-headline-sm text-lg font-semibold text-deep-river">
                {t('Booking Contact & E-Ticket Recipient')}
              </h3>
              <p className="text-xs text-on-surface-variant">
                {t('All travel documents and vessel dispatch notifications will be routed here')}
              </p>
            </div>
          </div>
          <label className="flex cursor-pointer items-center gap-3 rounded-xl bg-mist px-space-md py-space-md text-sm text-deep-river">
            <input
              type="checkbox"
              checked={passenger.useAsContact}
              onChange={(e) => onChange({ useAsContact: e.target.checked })}
              className="h-5 w-5 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow"
            />
            {t('Use passenger details as booking contact')}
          </label>
        </div>

      </BookingCard>

      <div className="flex items-center justify-between gap-3 rounded-2xl bg-white px-space-lg py-space-md text-xs text-on-surface-variant shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
        <span className="flex items-center gap-2">
          <Icon name="lock" className="text-[18px] text-teal-flow" />
          {t('Your passenger information is protected by 256-bit waterway security encryption.')}
        </span>
        <Link
          to={ROUTES.seatSelection}
          className="whitespace-nowrap font-semibold text-teal-flow hover:underline"
        >
          {t('Return to seat map')}
        </Link>
      </div>
    </div>
  )
}
