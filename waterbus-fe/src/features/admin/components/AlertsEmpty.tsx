import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/** Shown when no alert is open. */
export default function AlertsEmpty() {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-dashed border-outline-variant bg-white p-4">
      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-flow/10 text-teal-flow">
        <Icon name="check_circle" className="text-[22px]" />
      </span>
      <div>
        <p className="font-semibold text-deep-river">{t('No weather warning right now.')}</p>
        <p className="text-sm text-on-surface-variant">
          {t('New alerts from the weather check or a captain appear here by themselves.')}
        </p>
      </div>
    </div>
  )
}
