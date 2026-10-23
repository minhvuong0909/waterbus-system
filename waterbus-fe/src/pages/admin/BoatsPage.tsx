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
import { createBoat, deleteBoat, getBoats } from '../../features/admin/services/adminService'
import type { AdminBoat } from '../../features/admin/types'
import { totalSeatsOf } from '../../features/admin/utils'
import { useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { USE_MOCK } from '../../constant/env'

type Dialog = { kind: 'form' } | { kind: 'delete'; boat: AdminBoat } | null

const EMPTY = {
  code: '',
  name: '',
  registrationNumber: '',
  manufacturer: '',
  yearBuilt: String(new Date().getFullYear()),
  frontCabinSeats: USE_MOCK ? '12' : '0',
  standardSeats: USE_MOCK ? '36' : '60',
  outdoorSeats: USE_MOCK ? '12' : '0',
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
  for (const key of ['frontCabinSeats', 'standardSeats', 'outdoorSeats'] as const) {
    const count = Number(form[key])
    if (form[key] === '' || !Number.isInteger(count) || count < 0) errors[key] = t('Enter a whole number, 0 or more.')
  }
  if (!errors.frontCabinSeats && !errors.standardSeats && !errors.outdoorSeats) {
    if (Number(form.frontCabinSeats) + Number(form.standardSeats) + Number(form.outdoorSeats) < 1) {
      errors.standardSeats = t('A boat needs at least one seat.')
    }
  }
  return errors
}

function BoatForm({ onDone, onCancel }: { onDone: () => void; onCancel: () => void }) {
  const [form, setForm] = useState<FormState>(EMPTY)
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const errors = showErrors ? validate(form) : {}
  const set = (key: keyof FormState) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))
  const total = totalSeatsOf({
    frontCabinSeats: Number(form.frontCabinSeats) || 0,
    standardSeats: Number(form.standardSeats) || 0,
    outdoorSeats: Number(form.outdoorSeats) || 0,
  })

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
        frontCabinSeats: Number(form.frontCabinSeats),
        standardSeats: Number(form.standardSeats),
        outdoorSeats: Number(form.outdoorSeats),
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
      </div>
      <fieldset className="rounded-lg border border-outline-variant/50 p-3">
        <legend className="px-1 text-xs font-semibold text-on-surface-variant">{t('Seat layout')}</legend>
        <div className="grid grid-cols-3 gap-3">
          {USE_MOCK && <TextField label="Front cabin" inputMode="numeric" value={form.frontCabinSeats} onChange={set('frontCabinSeats')} error={errors.frontCabinSeats} />}
          <TextField label={USE_MOCK ? 'Standard' : 'Tổng số ghế'} inputMode="numeric" value={form.standardSeats} onChange={set('standardSeats')} error={errors.standardSeats} />
          {USE_MOCK && <TextField label="Outdoor deck" inputMode="numeric" value={form.outdoorSeats} onChange={set('outdoorSeats')} error={errors.outdoorSeats} />}
        </div>
        <p className="mt-2 text-xs text-on-surface-variant">
          {USE_MOCK ? t('Total seats: {total}. Seat codes are generated as F01.., S01.. and O01..', { total }) : 'Chỉ lưu tổng số ghế. Chưa có API cấu hình sơ đồ ghế cho tàu mới.'}
        </p>
      </fieldset>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Cancel')}
        </ActionButton>
        <ActionButton type="submit" disabled={busy}>
          {busy ? t('Saving…') : t('Save boat')}
        </ActionButton>
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
        <span title={t('Front cabin / standard / outdoor')}>
          {b.totalSeats} {USE_MOCK && <span className="text-on-surface-variant">({b.frontCabinSeats}/{b.standardSeats}/{b.outdoorSeats})</span>}
        </span>
      ),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (b) => (
        <ActionButton tone="secondary" icon="delete" onClick={() => { remove.clearError(); setDialog({ kind: 'delete', boat: b }) }}>
          {t('Deactivate')}
        </ActionButton>
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
          <ActionButton icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add boat')}
          </ActionButton>
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
            <ActionButton tone="secondary" onClick={() => setDialog(null)}>
              {t('Cancel')}
            </ActionButton>
            <ActionButton
              tone="danger"
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
            </ActionButton>
          </div>
        </Modal>
      )}
    </>
  )
}
