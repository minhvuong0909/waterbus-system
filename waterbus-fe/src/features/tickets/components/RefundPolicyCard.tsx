import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/**
 * What happens to the money if the trip does not run (BR v2.0 §10). Passengers do not file refund
 * requests: when a trip is cancelled or terminated early, the system refunds the paid amount of
 * the affected tickets to the original payment method.
 */
export default function RefundPolicyCard() {
  return (
    <div className="rounded-2xl bg-white p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.06)]">
      <h2 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
        <Icon name="payments" className="text-[22px] text-teal-flow" />
        {t('Refunds')}
      </h2>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t(
          'If this trip is cancelled or ends early before you reach your stop, the amount you paid for the affected tickets is refunded to your original payment method. Any refund appears here with its status.',
        )}
      </p>
      <p className="mt-space-sm text-sm text-on-surface-variant">
        {t('Tickets of passengers who miss check-in are not refunded.')}
      </p>
    </div>
  )
}
