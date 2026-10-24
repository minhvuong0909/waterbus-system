import { CopyButton, Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import { t } from '../../../i18n'

interface PaymentMethodPanelProps {
  amountVnd: number
  /** Booking code shown as the payment reference. */
  reference: string
}

/**
 * The only payment method is VNPay (BR-PAY): the traveler pays on the VNPay page, which offers QR,
 * bank card and e-wallet. The payment is confirmed by the backend, not by anything done here.
 */
export default function PaymentMethodPanel({ amountVnd, reference }: PaymentMethodPanelProps) {
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:p-space-xl">
      <h2 className="font-headline-lg text-headline-md text-deep-river">{t('Payment Method')}</h2>

      <div className="mt-space-md flex items-start gap-space-md rounded-2xl border-2 border-teal-flow bg-sand-light/50 p-space-lg">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-white text-teal-flow">
          <Icon name="account_balance" className="text-[26px]" />
        </span>
        <div>
          <h3 className="font-headline-sm text-xl font-bold text-deep-river">VNPay</h3>
          <p className="text-sm text-on-surface-variant">
            {t('Pay by QR code, bank card or e-wallet on the secure VNPay page.')}
          </p>
          <dl className="mt-space-md grid grid-cols-2 gap-x-space-lg gap-y-1 text-sm">
            <dt className="text-on-surface-variant">{t('Amount to pay')}</dt>
            <dd className="text-right font-bold text-deep-river">{formatVndSuffix(amountVnd)}</dd>
            <dt className="text-on-surface-variant">{t('Booking Reference')}</dt>
            <dd className="flex items-center justify-end gap-2 font-mono font-bold text-deep-river">
              {reference}
              <CopyButton value={reference} />
            </dd>
          </dl>
        </div>
      </div>

      <div className="mt-space-md flex items-start gap-3 rounded-xl bg-surface-container px-space-md py-space-md text-xs text-on-surface-variant">
        <span className="mt-1 h-2.5 w-2.5 shrink-0 rounded-full bg-teal-flow" />
        <div>
          <div className="font-semibold text-deep-river">
            {t('Payment status: Waiting for confirmation')}
          </div>
          {t('After completing payment, continue to check the booking status.')}
        </div>
      </div>
    </div>
  )
}
