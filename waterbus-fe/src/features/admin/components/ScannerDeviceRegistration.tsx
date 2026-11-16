import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, SelectField, TextField } from '../../../components/ui'
import { useSubmit } from '../../../hooks'
import { t } from '../../../i18n'
import type { AdminStation } from '../types'
import { registerScannerDevice } from '../services/scannerAssignmentService'

export default function ScannerDeviceRegistration({ stations, onRegistered }: {
  stations: AdminStation[]
  onRegistered: (id: string) => void
}) {
  const [stationId, setStationId] = useState('')
  const [label, setLabel] = useState('')
  const { run, busy, error } = useSubmit()

  async function create(event: FormEvent) {
    event.preventDefault()
    if (!stationId || busy) return
    const id = await run(() => registerScannerDevice(stationId, label.trim() || null))
    if (id) {
      setLabel('')
      onRegistered(id)
    }
  }

  return (
    <form className="space-y-3" onSubmit={create}>
      <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2">
        <SelectField label="Station" value={stationId} onChange={(event) => setStationId(event.target.value)} required>
          <option value="">{t('Choose a station')}</option>
          {stations.map((station) => <option key={station.id} value={station.id}>{station.name}</option>)}
        </SelectField>
        <TextField label="Device label (optional)" value={label} maxLength={100} onChange={(event) => setLabel(event.target.value)} />
      </fieldset>
      {error && <Notice tone="error">{error}</Notice>}
      <Button type="submit" disabled={!stationId || busy}>{t('Create scanner')}</Button>
    </form>
  )
}
