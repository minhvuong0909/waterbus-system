import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, DataTable, ErrorState, Modal, Notice, PageHeader, PageLoader, TextField } from '../../components/ui'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import { createBoat, deleteBoat, getBoats } from '../../features/admin/services/boatService'
import type { AdminBoat } from '../../features/admin/types'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import type { Column } from '../../components/ui'

type Dialog = { kind: 'form' } | { kind: 'delete'; boat: AdminBoat } | null

const EMPTY = {
  code: '',
  name: '',
  registrationNumber: '',
  manufacturer: '',
  yearBuilt: String(new Date().getFullYear()),
  totalSeats: '60',
}
type FormState = typeof EMPTY

function validate(form: FormState): Partial<Record<keyof FormState, string>> {
  const errors: Partial<Record<keyof FormState, string>> = {}
  if (!form.code.trim()) errors.code = t('Code is required.')
  if (!form.name.trim()) errors.name = t('Name is required.')
  if (!form.registrationNumber.trim()) errors.registrationNumber = t('Registration number is required.')
  const year = Number(form.yearBuilt)
  if (!Number.isInteger(year) || year < 1950 || year > new Date().getFullYear() + 1) {
    errors.yearBuilt = t('Enter a valid year.')
  }
  const seats = Number(form.totalSeats)
  if (!Number.isInteger(seats) || seats < 1) errors.totalSeats = t('A boat needs at least one seat.')
  return errors
}

function BoatForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [form, setForm] = useState<FormState>(EMPTY)
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
    const saved = await run(() =>
      createBoat({
        code: form.code.trim().toUpperCase(),
        name: form.name.trim(),
        registrationNumber: form.registrationNumber.trim(),
        manufacturer: form.manufacturer.trim(),
        yearBuilt: Number(form.yearBuilt),
        totalSeats: Number(form.totalSeats),
      }),
    )
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Code" value={form.code} onChange={set('code')} error={errors.code} placeholder="SWB-02" />
        <TextField label="Name" value={form.name} onChange={set('name')} error={errors.name} />
        <TextField label="Registration number" value={form.registrationNumber} onChange={set('registrationNumber')} error={errors.registrationNumber} />
        <TextField label="Manufacturer" value={form.manufacturer} onChange={set('manufacturer')} />
        <TextField label="Year built" inputMode="numeric" value={form.yearBuilt} onChange={set('yearBuilt')} error={errors.yearBuilt} />
        <TextField label="Total seats" inputMode="numeric" value={form.totalSeats} onChange={set('totalSeats')} error={errors.totalSeats} />
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <Button size="sm" variant="neutral" onClick={onCancel}>
          {t('Cancel')}
        </Button>
        <Button size="sm" type="submit" disabled={busy}>
          {busy ? t('Saving…') : t('Save boat')}
        </Button>
      </div>
    </form>
  )
}

export default function BoatsPage() {
  useDocumentTitle('Boats')
  const [version, reload] = useReload()
  const boats = useFetch(getBoats, [version])
  const [dialog, setDialog] = useState<Dialog>(null)
  const remove = useSubmit()

  if (boats.error) return <ErrorState onRetry={boats.retry} />
  if (boats.loading || !boats.data) return <PageLoader />

  const columns: Column<AdminBoat>[] = [
    { header: 'Code', render: (b) => <span className="font-mono font-semibold">{b.code}</span> },
    { header: 'Name', render: (b) => b.name },
    { header: 'Registration', render: (b) => b.registrationNumber },
    { header: 'Built', render: (b) => `${b.manufacturer} ${b.yearBuilt}`.trim() },
    {
      header: 'Seats',
      render: (b) => (
        <span>{b.totalSeats}</span>
      ),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (b) => (
        <Button size="sm" variant="neutral" icon="delete" onClick={() => { remove.clearError(); setDialog({ kind: 'delete', boat: b }) }}>
          {t('Deactivate')}
        </Button>
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
        title="Boats"
        description="The fleet and the seat layout each boat is created with."
        actions={
          <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add boat')}
          </Button>
        }
      />
      <DataTable columns={columns} rows={boats.data} rowKey={(b) => b.id} />

      {dialog?.kind === 'form' && (
        <Modal title="Add boat" onClose={() => setDialog(null)}>
          <BoatForm onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}

      {dialog?.kind === 'delete' && (
        <Modal title="Deactivate boat" onClose={() => setDialog(null)}>
          <p className="text-sm text-on-surface-variant">
            {t('Deactivate {name}? The boat will no longer be listed as active.', { name: dialog.boat.name })}
          </p>
          {remove.error && (
            <div className="mt-3">
              <Notice tone="error">{remove.error}</Notice>
            </div>
          )}
          <div className="mt-4 flex justify-end gap-2">
            <Button size="sm" variant="neutral" onClick={() => setDialog(null)}>
              {t('Cancel')}
            </Button>
            <Button size="sm"
              variant="danger"
              disabled={remove.busy}
              onClick={async () => {
                const done = await remove.run(async () => {
                  await deleteBoat(dialog.boat.id)
                  return true
                })
                if (done) closeAndReload()
              }}
            >
              {t('Deactivate')}
            </Button>
          </div>
        </Modal>
      )}
    </>
  )
}
