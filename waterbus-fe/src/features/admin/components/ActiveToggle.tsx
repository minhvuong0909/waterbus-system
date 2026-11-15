import { Button } from '../../../components/ui'
import { t } from '../../../i18n'

/** Thao tác trên dòng: tắt một mục danh mục (sau khi nơi gọi đã hỏi lại) hoặc bật lại. */
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
