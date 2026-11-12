import { Button, Notice } from '../../../components/ui'
import { t } from '../../../i18n'

/** Foot of an admin form: the server's refusal, if any, then Cancel and the submit button. */
export default function FormActions({
  error,
  busy,
  submitLabel = 'Save',
  disabled = false,
  onCancel,
}: {
  error: string | null
  busy: boolean
  submitLabel?: string
  disabled?: boolean
  onCancel: () => void
}) {
  return (
    <>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <Button size="sm" variant="neutral" onClick={onCancel}>
          {t('Cancel')}
        </Button>
        <Button size="sm" type="submit" disabled={busy || disabled}>
          {busy ? t('Saving…') : t(submitLabel)}
        </Button>
      </div>
    </>
  )
}
