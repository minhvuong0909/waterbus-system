import { CopyButton, Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/**
 * How the order is paid. VNPay is the only method (BR-PAY): the traveler pays on the VNPay page,
 * which offers QR, bank card and e-wallet, and the backend confirms the payment, not this page.
 * The amount is in the order summary beside it, so it is not repeated here.
 */
export default function PaymentMethodPanel({ reference }: { /** Booking code(s) shown as the payment reference. */ reference: string }) {
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-card">
      <div className="flex items-start gap-space-md">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-secondary-container text-teal-flow">
          <Icon name="account_balance" className="text-2xl" />
        </span>
        <div>
          <h2 className="font-display text-headline-sm text-deep-river">VNPay</h2>
          <p className="mt-1 text-sm text-on-surface-variant">
            {t('Pay by QR code, bank card or e-wallet on the secure VNPay page.')}{' '}
            {t('After paying you come back here to see the result.')}
          </p>
        </div>
      </div>

      <dl className="mt-space-lg flex flex-wrap items-center justify-between gap-2 border-t border-surface-container pt-space-md text-sm">
        <dt className="text-on-surface-variant">{t('Booking Reference')}</dt>
        <dd className="flex items-center gap-2 font-mono font-bold text-deep-river">
          {reference}
          <CopyButton value={reference} />
        </dd>
      </dl>
    </div>
  )
}
