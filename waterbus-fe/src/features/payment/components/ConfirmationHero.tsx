import { CopyButton } from '../../../components/ui'
import { t } from '../../../i18n'

export default function ConfirmationHero({ reference, email }: { reference: string; email: string }) {
  return (
    <div>
      <div className="max-w-2xl space-y-space-md">
        <span className="inline-flex items-center gap-2 rounded-full bg-secondary-container/70 px-3 py-1 text-xs font-semibold text-on-secondary-container">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Payment Received • Booking Confirmed')}
        </span>
        <h1 className="font-display text-4xl font-bold tracking-tight text-deep-river md:text-headline-xl">
          {t('Your Journey Is Confirmed')}
        </h1>
        <p className="max-w-lg text-body-lg text-on-surface-variant">
          {t('Your e-tickets are ready. Show the QR code to staff when you board.')}
        </p>
        <div className="inline-flex items-center gap-3 rounded-xl border border-outline-variant/40 bg-white px-space-md py-2 shadow-sm">
          <span className="text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
            {t('Booking Ref:')}
          </span>
          <span className="font-display text-base font-bold text-deep-river">{reference}</span>
          <CopyButton value={reference} />
        </div>
        <p className="text-sm text-on-surface-variant">
          {/* The backend sends no e-mail yet: point to where the tickets are instead of promising one. */}
          {t('Keep this booking reference. Your tickets for {email} are saved under My Tickets on this device.', {
            email,
          })}
        </p>
      </div>
    </div>
  )
}
