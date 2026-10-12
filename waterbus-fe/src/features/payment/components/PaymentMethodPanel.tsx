import { useRef } from 'react'
import { CopyButton, ErrorState, Icon } from '../../../components/ui'
import QrCode from '../../../components/ticket/QrCode'
import { useFetch } from '../../../hooks'
import { getEwallets, getPaymentMethods } from '../services/paymentService'
import type { PaymentMethodId } from '../types'
import { formatVndSuffix } from '../../../utils/format'
import { t } from '../../../i18n'

interface PaymentMethodPanelProps {
  tripCode: string
  method: PaymentMethodId
  onMethod: (id: PaymentMethodId) => void
  amountVnd: number
  reference: string
}

const INNER = 'rounded-2xl bg-mist/70 p-space-lg'
const FIELD =
  'w-full rounded-xl border-0 bg-white py-3 text-sm text-deep-river placeholder:text-outline focus:ring-2 focus:ring-teal-flow/30'

function QrPanel({
  amountVnd,
  reference,
  onOther,
}: {
  amountVnd: number
  reference: string
  onOther: () => void
}) {
  return (
    <div className={INNER}>
      <h3 className="font-headline-sm text-xl font-bold text-deep-river">
        {t('Scan to Pay with Any Banking App')}
      </h3>
      <p className="text-xs text-on-surface-variant">
        {t('Compatible with major banking and mobile payment applications.')}
      </p>
      <div className="mt-space-md flex flex-col gap-space-lg md:flex-row md:items-center">
        <div className="rounded-2xl bg-white p-space-md text-center shadow-sm">
          <QrCode value={`${reference}-${amountVnd}`} size={170} className="mx-auto" />
          <div className="mt-2 text-[11px] text-on-surface-variant">{t('Amount to pay')}</div>
          <div className="font-headline-sm text-lg font-bold text-deep-river">
            {formatVndSuffix(amountVnd)}
          </div>
        </div>
        <div className="flex-1 space-y-space-md">
          <p className="text-sm text-on-surface-variant">
            {t('Scan this QR code using your preferred banking or payment app.')}
          </p>
          <div className="rounded-xl bg-white/70 p-space-md">
            <div className="flex items-center justify-between text-xs text-on-surface-variant">
              {t('Booking Reference')}
              <CopyButton value={reference} />
            </div>
            <div className="mt-1 font-headline-sm text-xl font-bold tracking-wide text-deep-river">
              {reference}
            </div>
          </div>
        </div>
      </div>
      <button
        type="button"
        onClick={onOther}
        className="mt-space-md text-xs font-semibold text-teal-flow hover:underline"
      >
        {t('Choose another payment method')}
      </button>
    </div>
  )
}

function CardPanel() {
  return (
    <div className={INNER}>
      <h3 className="font-headline-sm text-xl font-bold text-deep-river">
        {t('Pay with Bank Card')}
      </h3>
      <p className="text-xs text-on-surface-variant">
        {t('Visa, Mastercard and domestic cards are accepted.')}
      </p>
      <div className="mt-space-md grid grid-cols-1 gap-space-sm md:grid-cols-2">
        <label className="md:col-span-2">
          <span className="mb-1 block text-xs font-semibold text-on-surface">
            {t('Card number')}
          </span>
          <input
            className={`${FIELD} px-3`}
            placeholder="4242 4242 4242 4242"
            inputMode="numeric"
          />
        </label>
        <label>
          <span className="mb-1 block text-xs font-semibold text-on-surface">{t('Expiry')}</span>
          <input className={`${FIELD} px-3`} placeholder={t('MM / YY')} />
        </label>
        <label>
          <span className="mb-1 block text-xs font-semibold text-on-surface">{t('CVC')}</span>
          <input className={`${FIELD} px-3`} placeholder="123" inputMode="numeric" />
        </label>
      </div>
      <p className="mt-space-sm text-xs text-on-surface-variant">
        {t('Demo only: nothing is charged and card details are not stored.')}
      </p>
    </div>
  )
}

function WalletPanel() {
  const { data, error, retry } = useFetch(getEwallets)
  return (
    <div className={INNER}>
      <h3 className="font-headline-sm text-xl font-bold text-deep-river">
        {t('Pay with E-Wallet')}
      </h3>
      <p className="text-xs text-on-surface-variant">
        {t('Choose your mobile wallet to continue.')}
      </p>
      {error && (
        <div className="mt-space-md">
          <ErrorState compact onRetry={retry} />
        </div>
      )}
      <ul className="mt-space-md space-y-space-sm">
        {(data ?? []).map((w) => (
          <li
            key={w.id}
            className="flex items-center justify-between rounded-xl bg-white px-space-md py-space-sm shadow-sm"
          >
            <span>
              <span className="block text-sm font-semibold text-deep-river">{w.name}</span>
              <span className="text-xs text-on-surface-variant">{w.note}</span>
            </span>
            <Icon name="chevron_right" className="text-[20px] text-outline" />
          </li>
        ))}
      </ul>
    </div>
  )
}

export default function PaymentMethodPanel({
  tripCode,
  method,
  onMethod,
  amountVnd,
  reference,
}: PaymentMethodPanelProps) {
  const { data: methods, error: methodsError, retry: retryMethods } = useFetch(getPaymentMethods)
  const groupRef = useRef<HTMLDivElement>(null)

  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:p-space-xl">
      <h2 className="font-headline-lg text-headline-md text-deep-river">{t('Payment Method')}</h2>
      <p className="text-sm text-on-surface-variant">
        {t('Select how you would like to pay for trip {tripCode}', { tripCode })}
      </p>

      {methodsError && (
        <div className="mt-space-md">
          <ErrorState compact onRetry={retryMethods} />
        </div>
      )}

      <div
        ref={groupRef}
        role="radiogroup"
        aria-label={t('Payment method')}
        className="mt-space-md grid grid-cols-1 gap-space-sm md:grid-cols-3"
      >
        {(methods ?? []).map((m) => {
          const active = method === m.id
          return (
            <button
              key={m.id}
              type="button"
              role="radio"
              aria-checked={active}
              onClick={() => onMethod(m.id)}
              className={`relative rounded-2xl border-2 p-space-md text-left transition-all ${
                active
                  ? 'border-teal-flow bg-sand-light/50 shadow-sm'
                  : 'border-transparent bg-white shadow-[0_1px_6px_rgba(13,37,56,0.06)] hover:border-teal-flow/30'
              }`}
            >
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-mist text-deep-river">
                <Icon name={m.icon} className="text-[22px]" />
              </span>
              <span
                className={`absolute right-3 top-3 flex h-5 w-5 items-center justify-center rounded-full ${
                  active ? 'bg-teal-flow' : 'bg-surface-container'
                }`}
              >
                {active && <span className="h-2 w-2 rounded-full bg-white" />}
              </span>
              <span className="mt-space-md block font-headline-sm text-lg font-bold text-deep-river">
                {m.title}
              </span>
              <span className="text-xs text-on-surface-variant">{m.description}</span>
            </button>
          )
        })}
      </div>

      <div className="mt-space-md">
        {method === 'qr' && (
          <QrPanel
            amountVnd={amountVnd}
            reference={reference}
            onOther={() =>
              groupRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
            }
          />
        )}
        {method === 'card' && <CardPanel />}
        {method === 'wallet' && <WalletPanel />}
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
      <p className="mt-space-sm flex items-center gap-2 text-xs text-on-surface-variant">
        <Icon name="verified_user" className="text-[16px] text-teal-flow" />
        {t(
          'Payment details are handled securely. Your ticket will become available after the booking is successfully confirmed.',
        )}
      </p>
    </div>
  )
}
