import { ROUTES } from '../../../routes/routes'
import type { PurchaserForm, Seat, SeatPassengerForm } from '../types'
import BookingCard from './BookingCard'
import CardHeading from './CardHeading'
import { t } from '../../../i18n'
import EditLink from './EditLink'

export default function PassengerInfoCard({
  purchaser,
  seats,
  passengers,
  title,
  showBuyer = true,
}: {
  purchaser: PurchaserForm
  seats: Seat[]
  passengers: Record<string, SeatPassengerForm>
  /** Heading, e.g. "Passengers · return trip"; "Passenger Information" by default. */
  title?: string
  /** False hides the buyer block (shown once for a round trip). */
  showBuyer?: boolean
}) {
  return (
    <BookingCard>
      <CardHeading
        icon="badge"
        title={title ?? t('Passenger Information')}
        subtitle={t('One ticket per passenger, issued in each passenger’s name')}
        aside={<EditLink to={ROUTES.passengerDetails}>{t('Edit Passenger Details')}</EditLink>}
      />
      <ul className="space-y-space-sm">
        {seats.map((seat, index) => {
          const passenger = passengers[seat.id]
          const contact = [passenger?.email, passenger?.phone].filter(Boolean).join(' • ')
          return (
            <li
              key={seat.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-mist/70 p-space-md"
            >
              <div>
                <div className="text-xs text-on-surface-variant">
                  {t('Passenger {number}', { number: index + 1 })}
                </div>
                <div className="text-sm font-semibold text-deep-river">
                  {passenger?.fullName ?? ''}
                </div>
                {contact && <div className="text-xs text-on-surface-variant">{contact}</div>}
              </div>
              <span className="rounded bg-surface-container px-2 py-0.5 text-xs font-semibold text-deep-river">
                {t('Seat {id}', { id: seat.code })}
              </span>
            </li>
          )
        })}
      </ul>
      {showBuyer && (
        <div className="mt-space-md grid grid-cols-1 gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-2">
          <div>
            <div className="text-xs text-on-surface-variant">{t('Buyer')}</div>
            <div className="text-sm font-semibold text-deep-river">{purchaser.fullName}</div>
          </div>
          <div>
            <div className="text-xs text-on-surface-variant">{t('Buyer email')}</div>
            <div className="text-sm font-semibold text-deep-river">{purchaser.email}</div>
          </div>
          {purchaser.phone.trim() && (
            <div>
              <div className="text-xs text-on-surface-variant">{t('Contact Mobile')}</div>
              <div className="text-sm font-semibold text-deep-river">
                {purchaser.phoneCode} {purchaser.phone}
              </div>
            </div>
          )}
        </div>
      )}
    </BookingCard>
  )
}
