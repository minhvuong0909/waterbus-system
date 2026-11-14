import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, TextField } from '../../../components/ui'
import { useSubmit } from '../../../hooks'
import { t } from '../../../i18n'
import { registerScannerDevice } from '../services/scannerAssignmentService'

export default function ScannerDeviceRegistration({ disabled, onRegistered }: {
  disabled: boolean
  onRegistered: (id: string) => void
}) {
  const [open, setOpen] = useState(false)
  const [code, setCode] = useState('')
  const [label, setLabel] = useState('')
  const submitting = useRef(false)
  const { run, busy, error } = useSubmit()
  const validCode = /^[A-Za-z0-9._-]{1,50}$/.test(code.trim())

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (disabled || submitting.current || !validCode) return
    submitting.current = true
    try {
      const id = await run(() => registerScannerDevice(code.trim(), label.trim() || null))
      if (!id) return
      setOpen(false)
      setCode('')
      setLabel('')
      onRegistered(id)
    } finally {
      submitting.current = false
    }
  }

  if (!open) return <Button variant="neutral" disabled={disabled} onClick={() => setOpen(true)}>{t('Register a scanner')}</Button>

  return (
    <form onSubmit={submit} className="space-y-3 rounded-xl border border-outline-variant/40 p-4">
      <TextField label="Scanner code" maxLength={50} value={code} onChange={(event) => setCode(event.target.value)}
        hint={t('Use letters, numbers, dots, underscores or hyphens.')} disabled={busy || disabled} />
      <TextField label="Device label" maxLength={100} value={label} onChange={(event) => setLabel(event.target.value)}
        disabled={busy || disabled} />
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex gap-2">
        <Button type="submit" disabled={busy || disabled || !validCode}>{t('Register a scanner')}</Button>
        <Button variant="neutral" disabled={busy} onClick={() => setOpen(false)}>{t('Cancel')}</Button>
      </div>
    </form>
  )
}
