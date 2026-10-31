import type { PurchaserForm } from '../types'
import { purchaserErrors } from '../utils'
import BookingCard from './BookingCard'
import { t } from '../../../i18n'
import FormField from './FormField'
import FormTextField from './FormTextField'
import FormSectionTitle from './FormSectionTitle'
import { FORM_INPUT } from '../formStyles'

interface BuyerCardProps {
  purchaser: PurchaserForm
  onPurchaser: (patch: Partial<PurchaserForm>) => void
  /** "I am also a passenger": the buyer takes the first chosen seat. */
  buyerTravels: boolean
  onBuyerTravels: (value: boolean) => void
  /** Code of the seat the buyer takes when travelling. */
  seatCode?: string
}

/** Who pays (BR-COM-07), asked first: most buyers book for themselves. */
export default function BuyerCard({
  purchaser,
  onPurchaser,
  buyerTravels,
  onBuyerTravels,
  seatCode,
}: BuyerCardProps) {
  const buyerErrors = purchaserErrors(purchaser)
  return (
    <BookingCard className="p-space-lg md:p-space-xl">
      <FormSectionTitle
        icon="contact_mail"
        title={t('Buyer')}
        subtitle={t('The person who pays for this order')}
      />
      <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
        <FormTextField
          label={t('Full Name')}
          icon="badge"
          value={purchaser.fullName}
          error={buyerErrors.fullName}
          onChange={(fullName) => onPurchaser({ fullName })}
          autoComplete="name"
          required
        />
        <FormTextField
          label={t('Email Address')}
          icon="mail"
          type="email"
          value={purchaser.email}
          error={buyerErrors.email}
          onChange={(email) => onPurchaser({ email })}
          autoComplete="email"
          required
        />
        <FormField label={t('Phone Number')} hint={t('Optional')} error={undefined}>
          <span className="flex gap-2">
            <span className="flex items-center rounded-xl bg-mist px-3 text-sm text-deep-river">
              <span className="mr-1 text-xs text-on-surface-variant">{t('VN')}</span>
              {purchaser.phoneCode}
            </span>
            <input
              className={`${FORM_INPUT} px-3`}
              value={purchaser.phone}
              onChange={(e) => onPurchaser({ phone: e.target.value })}
              autoComplete="tel-national"
              aria-invalid={buyerErrors.phone ? true : undefined}
            />
          </span>
        </FormField>
      </div>
      {buyerErrors.phone && (
        <p role="alert" className="mt-1 text-xs text-coral-glow">
          {buyerErrors.phone}
        </p>
      )}
      <label className="mt-space-md flex cursor-pointer items-center gap-3 rounded-xl border border-outline-variant/50 px-3 py-3 text-sm text-deep-river">
        <input
          type="checkbox"
          checked={buyerTravels}
          onChange={(e) => onBuyerTravels(e.target.checked)}
          className="h-4 w-4 rounded border-outline text-teal-flow focus:ring-teal-flow/30"
        />
        <span>
          <span className="font-semibold">{t('I am also a passenger')}</span>
          {seatCode && (
            <span className="text-on-surface-variant"> · {t('Seat {id}', { id: seatCode })}</span>
          )}
        </span>
      </label>
    </BookingCard>
  )
}
