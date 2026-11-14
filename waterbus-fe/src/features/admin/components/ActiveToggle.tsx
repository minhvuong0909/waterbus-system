import { Button } from '../../../components/ui'
import { t } from '../../../i18n'

/** Row action that switches a catalogue item off (after a confirmation by the caller) or back on. */
export default function ActiveToggle({
  active,
  busy,
  onActivate,
  onDeactivate,
}: {
  active: boolean
  busy: boolean
  onActivate: () => void
  onDeactivate: () => void
}) {
  return active ? (
    <Button size="sm" variant="neutral" onClick={onDeactivate}>
      {t('Switch off')}
    </Button>
  ) : (
    <Button size="sm" variant="neutral" disabled={busy} onClick={onActivate}>
      {t('Switch on')}
    </Button>
  )
}
