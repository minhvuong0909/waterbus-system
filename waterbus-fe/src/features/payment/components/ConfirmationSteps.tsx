import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

const CHIPS = ['Trip', 'Seat', 'Details', 'Checkout']

/** Slim bar under the breadcrumb: four ticks and the highlighted "Confirmed" step. */
export default function ConfirmationSteps() {
  return (
    <div className="flex flex-wrap items-center gap-2 text-[11px] font-semibold text-teal-flow">
      {CHIPS.map((label, i) => (
        <span key={label} className="flex items-center gap-2">
          <span className="inline-flex items-center gap-1 rounded-full bg-sand-light px-2.5 py-1">
            <Icon name="check_circle" className="text-[13px]" />
            {String(i + 1).padStart(2, '0')} {t(label)}
          </span>
          <span className="hidden h-px w-3 bg-outline-variant sm:inline-block" />
        </span>
      ))}
      <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-1 text-on-secondary-container ring-1 ring-teal-flow/30">
        <Icon name="check_circle" className="text-[13px]" />
        {t('05 Confirmed')}
      </span>
    </div>
  )
}
