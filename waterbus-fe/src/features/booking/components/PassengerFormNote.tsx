import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/** Closing line under the passenger cards; the way back to the seat map is in the order summary. */
export default function PassengerFormNote() {
  return (
    <p className="flex items-center gap-2 px-1 text-xs text-on-surface-variant">
      <Icon name="info" className="text-lg text-teal-flow" />
      {t('Each passenger gets a ticket in their own name.')}
    </p>
  )
}
