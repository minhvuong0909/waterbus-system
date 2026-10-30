import type { ReactNode } from 'react'
import { Icon } from '../../../components/ui'
import type { PurchaserForm, Seat, SeatPassengerForm } from '../types'
import { seatPassengerErrors, seatTags } from '../utils'
import BookingCard from './BookingCard'
import { t } from '../../../i18n'
import FormTextField from './FormTextField'
import FormSectionTitle from './FormSectionTitle'

interface PassengerFormCardProps {
  purchaser: PurchaserForm
  /** The chosen seats; each one needs its own passenger (BR-COM-06). */
  seats: Seat[]
  /** Passenger of each seat, by seat id. */
  passengers: Record<string, SeatPassengerForm>
  onPassenger: (seatId: string, patch: Partial<SeatPassengerForm>) => void
  /** The seat the buyer travels on: its details come from the buyer card, so they are not asked again. */
  buyerSeatId?: string
  /** Shown above the passengers, e.g. which leg of a round trip they travel on. */
  heading?: ReactNode
}

export default function PassengerFormCard({
  purchaser,
  seats,
  passengers,
  onPassenger,
  buyerSeatId,
  heading,
}: PassengerFormCardProps) {
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
              <FormSectionTitle
                icon="person"
                title={t('Passenger {number}', { number: index + 1 })}
                subtitle={t('Who will travel on this seat')}
              />
              <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
                <Icon name="airline_seat_recline_extra" className="text-sm" />
                {t('Seat {id}', { id: seat.code })}
                {tags && ` • ${tags}`}
              </span>
            </div>
            {seat.id === buyerSeatId ? (
              <p className="flex items-center gap-2 rounded-xl bg-mist px-3 py-3 text-sm text-deep-river">
                <Icon name="badge" className="text-lg text-outline" />
                {purchaser.fullName.trim()
                  ? t('{name} (the buyer)', { name: purchaser.fullName.trim() })
                  : t('Filled in from the buyer details')}
              </p>
            ) : (
              <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
                <FormTextField
                  label={t('Full Name')}
                  icon="badge"
                  value={passenger?.fullName ?? ''}
                  error={errors.fullName}
                  onChange={(fullName) => onPassenger(seat.id, { fullName })}
                  autoComplete="off"
                  required
                />
                <FormTextField
                  label={t('Email Address')}
                  hint={t('Optional')}
                  icon="mail"
                  type="email"
                  value={passenger?.email ?? ''}
                  error={errors.email}
                  onChange={(email) => onPassenger(seat.id, { email })}
                />
                <FormTextField
                  label={t('Phone Number')}
                  hint={t('Optional')}
                  value={passenger?.phone ?? ''}
                  onChange={(phone) => onPassenger(seat.id, { phone })}
                  autoComplete="off"
                />
              </div>
            )}
          </BookingCard>
        )
      })}
    </div>
  )
}
