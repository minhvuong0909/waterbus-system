import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  ActionButton,
  DataTable,
  Modal,
  Notice,
  PageHeader,
  TextField,
} from '../../features/admin/components/AdminUi'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  createStation,
  deleteStation,
  getStations,
  updateStation,
} from '../../features/admin/services/adminService'
import type { AdminStation, NewStation } from '../../features/admin/types'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'

type Dialog = { kind: 'form'; station?: AdminStation } | { kind: 'delete'; station: AdminStation } | null

interface FormState {
  code: string
  name: string
  address: string
  latitude: string
  longitude: string
  orderIndex: string
}

const toForm = (s?: AdminStation): FormState => ({
  code: s?.code ?? '',
  name: s?.name ?? '',
  address: s?.address ?? '',
  latitude: s ? String(s.latitude) : '',
  longitude: s ? String(s.longitude) : '',
  orderIndex: s ? String(s.orderIndex) : '',
})

/** Field errors for the form, or null when it can be submitted. */
function validate(form: FormState): Partial<Record<keyof FormState, string>> {
  const errors: Partial<Record<keyof FormState, string>> = {}
  if (!form.code.trim()) errors.code = t('Code is required.')
  if (!form.name.trim()) errors.name = t('Name is required.')
  if (!form.address.trim()) errors.address = t('Address is required.')
  const lat = Number(form.latitude)
  const lng = Number(form.longitude)
  if (form.latitude === '' || !(lat >= -90 && lat <= 90)) errors.latitude = t('Enter a latitude between -90 and 90.')
  if (form.longitude === '' || !(lng >= -180 && lng <= 180)) errors.longitude = t('Enter a longitude between -180 and 180.')
  const order = Number(form.orderIndex)
  if (!Number.isInteger(order) || order < 1) errors.orderIndex = t('Enter a whole number from 1.')
  return errors
}

function StationForm({
  station,
  onDone,
  onCancel,
}: {
  station?: AdminStation
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState(() => toForm(station))
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const errors = showErrors ? validate(form) : {}
  const set = (key: keyof FormState) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.keys(validate(form)).length > 0) {
      setShowErrors(true)
      return
    }
    const input: NewStation = {
      code: form.code.trim().toUpperCase(),
      name: form.name.trim(),
      address: form.address.trim(),
      latitude: Number(form.latitude),
      longitude: Number(form.longitude),
      orderIndex: Number(form.orderIndex),
    }
    const saved = await run(() => (station ? updateStation(station.id, input) : createStation(input)))
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Code" value={form.code} onChange={set('code')} error={errors.code} placeholder="ST06" />
        <TextField
          label="River order"
          hint="Position along the river, 1 is the first station."
          inputMode="numeric"
          value={form.orderIndex}
          onChange={set('orderIndex')}
          error={errors.orderIndex}
        />
      </div>
      <TextField label="Name" value={form.name} onChange={set('name')} error={errors.name} />
      <TextField label="Address" value={form.address} onChange={set('address')} error={errors.address} />
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Latitude" inputMode="decimal" value={form.latitude} onChange={set('latitude')} error={errors.latitude} />
        <TextField label="Longitude" inputMode="decimal" value={form.longitude} onChange={set('longitude')} error={errors.longitude} />
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Cancel')}
        </ActionButton>
        <ActionButton type="submit" disabled={busy}>
          {busy ? t('Saving…') : t('Save station')}
        </ActionButton>
      </div>
    </form>
  )
}

export default function StationsPage() {
  useDocumentTitle('Stations')
  const [version, reload] = useReload()
  const stations = useFetch(getStations, [version])
  const [dialog, setDialog] = useState<Dialog>(null)
  const remove = useSubmit()

  if (stations.error) return <ErrorState onRetry={stations.retry} />
  if (stations.loading || !stations.data) return <PageLoader />

  const columns: Column<AdminStation>[] = [
    { header: 'Order', render: (s) => s.orderIndex, className: 'w-16' },
    { header: 'Code', render: (s) => <span className="font-mono font-semibold">{s.code}</span> },
    { header: 'Name', render: (s) => s.name },
    { header: 'Address', render: (s) => <span className="text-on-surface-variant">{s.address}</span> },
    { header: 'Coordinates', render: (s) => `${s.latitude.toFixed(4)}, ${s.longitude.toFixed(4)}` },
    {
      header: 'Actions',
      className: 'text-right',
      render: (s) => (
        <span className="inline-flex gap-1">
          <ActionButton tone="secondary" icon="edit" onClick={() => setDialog({ kind: 'form', station: s })}>
            {t('Edit')}
          </ActionButton>
          <ActionButton tone="secondary" icon="delete" onClick={() => { remove.clearError(); setDialog({ kind: 'delete', station: s }) }}>
            {t('Deactivate')}
          </ActionButton>
        </span>
      ),
    },
  ]

  const closeAndReload = () => {
    setDialog(null)
    reload()
  }

  return (
    <>
      <PageHeader
        title="Stations"
        description="Piers along the Saigon River. Passengers pick from these when searching for a trip."
        actions={
          <ActionButton icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add station')}
          </ActionButton>
        }
      />
      <DataTable columns={columns} rows={stations.data} rowKey={(s) => s.id} />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.station ? 'Edit station' : 'Add station'} onClose={() => setDialog(null)}>
          <StationForm station={dialog.station} onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}

      {dialog?.kind === 'delete' && (
        <Modal title="Deactivate station" onClose={() => setDialog(null)}>
          <p className="text-sm text-on-surface-variant">
            {t('Deactivate {name}? Passengers will no longer see this station.', { name: dialog.station.name })}
          </p>
          {remove.error && (
            <div className="mt-3">
              <Notice tone="error">{remove.error}</Notice>
            </div>
          )}
          <div className="mt-4 flex justify-end gap-2">
            <ActionButton tone="secondary" onClick={() => setDialog(null)}>
              {t('Cancel')}
            </ActionButton>
            <ActionButton
              tone="danger"
              disabled={remove.busy}
              onClick={async () => {
                const done = await remove.run(async () => {
                  await deleteStation(dialog.station.id)
                  return true
                })
                if (done) closeAndReload()
              }}
            >
              {t('Deactivate')}
            </ActionButton>
          </div>
        </Modal>
      )}
    </>
  )
}
