import { Button, Modal, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../../../hooks'

interface ConfirmModalProps {
  title: string
  /** Already translated; says what the action changes. */
  message: string
  confirmLabel: string
  action: () => Promise<unknown>
  onDone: () => void
  onClose: () => void
}

/** Asks before switching something off; stays open with the server's reason when it is refused. */
export default function ConfirmModal({
  title,
  message,
  confirmLabel,
  action,
  onDone,
  onClose,
}: ConfirmModalProps) {
  const { run, busy, error } = useSubmit()

  const confirm = async () => {
    const done = await run(async () => {
      await action()
      return true
    })
    if (done) onDone()
  }

  return (
    <Modal title={title} onClose={onClose}>
      <p className="text-sm text-on-surface-variant">{message}</p>
      {error && (
        <div className="mt-3">
          <Notice tone="error">{error}</Notice>
        </div>
      )}
      <div className="mt-4 flex justify-end gap-2">
        <Button size="sm" variant="neutral" onClick={onClose}>
          {t('Cancel')}
        </Button>
        <Button size="sm" variant="danger" disabled={busy} onClick={confirm}>
          {t(confirmLabel)}
        </Button>
      </div>
    </Modal>
  )
}
