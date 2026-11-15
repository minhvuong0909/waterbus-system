import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, DataTable, ErrorState, FilterBar, FilterChips, Modal, PageHeader, PageLoader, TextField } from '../../components/ui'
import ActiveToggle from '../../features/admin/components/ActiveToggle'
import ConfirmModal from '../../features/admin/components/ConfirmModal'
import FormActions from '../../features/admin/components/FormActions'
import { ActiveBadge } from '../../features/admin/components/StatusBadge'
import { createBoat, setBoatActive, updateBoat } from '../../features/admin/services/boatService'
import { getBoats } from '../../features/operations/services/fleetService'
import type { AdminBoat } from '../../features/admin/types'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import type { Column } from '../../components/ui'

type Dialog = { kind: 'form'; boat?: AdminBoat } | { kind: 'deactivate'; boat: AdminBoat } | null

const EMPTY = {
  code: '',
  name: '',
  registrationNumber: '',
  manufacturer: '',
  yearBuilt: String(new Date().getFullYear()),
  totalSeats: '60',
}
type FormState = typeof EMPTY

/** `configuredSeats`: số ghế đang hoạt động; sức chứa khai báo không được thấp hơn (backend từ chối). */
function validate(form: FormState, configuredSeats = 0): Partial<Record<keyof FormState, string>> {
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
  else if (seats < configuredSeats) {
    errors.totalSeats = t('The boat already has {count} active seats. Deactivate some seats first.', { count: configuredSeats })
  }
  return errors
}

function BoatForm({ boat, onDone, onCancel }: { boat?: AdminBoat; onDone: () => void; onCancel: () => void }) {
  const [form, setForm] = useState<FormState>(() =>
    boat
      ? {
          code: boat.code,
          name: boat.name,
          registrationNumber: boat.registrationNumber,
          manufacturer: boat.manufacturer,
          yearBuilt: String(boat.yearBuilt),
          totalSeats: String(boat.totalSeats),
        }
      : EMPTY,
  )
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const errors = showErrors ? validate(form, boat?.configuredSeats) : {}
  const set = (key: keyof FormState) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.keys(validate(form, boat?.configuredSeats)).length > 0) {
      setShowErrors(true)
      return
    }
    const details = {
      name: form.name.trim(),
      registrationNumber: form.registrationNumber.trim(),
      manufacturer: form.manufacturer.trim(),
      yearBuilt: Number(form.yearBuilt),
      totalSeats: Number(form.totalSeats),
    }
    const saved = await run(() =>
      boat
        ? updateBoat(boat.id, details).then(() => true)
        : createBoat({ code: form.code.trim().toUpperCase(), ...details }).then(() => true),
    )
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Code" value={form.code} onChange={set('code')} error={errors.code} placeholder="SWB-02" maxLength={20} disabled={Boolean(boat)} hint={boat ? 'The code cannot be changed after the boat is created.' : undefined} />
        <TextField label="Name" value={form.name} onChange={set('name')} error={errors.name} maxLength={100} />
        <TextField label="Registration number" value={form.registrationNumber} onChange={set('registrationNumber')} error={errors.registrationNumber} maxLength={50} />
        <TextField label="Manufacturer" value={form.manufacturer} onChange={set('manufacturer')} />
        <TextField label="Year built" inputMode="numeric" value={form.yearBuilt} onChange={set('yearBuilt')} error={errors.yearBuilt} />
        <TextField label="Total seats" inputMode="numeric" value={form.totalSeats} onChange={set('totalSeats')} error={errors.totalSeats} />
      </div>
      <FormActions error={error} busy={busy} submitLabel="Save boat" onCancel={onCancel} />
    </form>
  )
}

export default function BoatsPage() {
  useDocumentTitle('Boats')
  const [version, reload] = useReload()
  // Tải HẾT (cả active lẫn inactive) - trước đây luôn gọi isActiveOnly:true, nên 1 tàu bị ngừng
  // biến mất hoàn toàn khỏi trang, không có cách nào xem lại hay bật lại qua UI này.
  const boats = useFetch(() => getBoats(false), [version])
  const [dialog, setDialog] = useState<Dialog>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const activate = useSubmit()

  if (boats.error) return <ErrorState onRetry={boats.retry} />
  if (boats.loading || !boats.data) return <PageLoader />

  const needle = search.trim().toLowerCase()
  const filteredBoats = boats.data.filter(
    (b) =>
      (needle === '' || b.code.toLowerCase().includes(needle) || b.name.toLowerCase().includes(needle)) &&
      (status === '' || (status === 'active') === b.isActive),
  )

  const columns: Column<AdminBoat>[] = [
    { header: 'Code', render: (b) => <span className="font-mono font-semibold">{b.code}</span> },
    { header: 'Name', render: (b) => b.name },
    { header: 'Registration', render: (b) => b.registrationNumber },
    { header: 'Built', render: (b) => `${b.manufacturer} ${b.yearBuilt}`.trim() },
    {
      header: 'Seats',
      // Số ghế đã thiết lập trên tổng sức chứa; tàu chưa có ghế thì không bán được (BR-SCH-04).
      render: (b) =>
        b.configuredSeats === undefined ? (
          b.totalSeats
        ) : b.configuredSeats === 0 ? (
          <span className="font-semibold text-coral-glow" title={t('No seats set up yet, so trips on this boat cannot be sold.')}>
            {t('No seats yet')} · {b.totalSeats}
          </span>
        ) : (
          <span title={t('Seats set up / declared capacity')}>{`${b.configuredSeats} / ${b.totalSeats}`}</span>
        ),
    },
    { header: 'Status', render: (b) => <ActiveBadge active={b.isActive} /> },
    {
      header: 'Actions',
      className: 'text-right',
      render: (b) => (
        <span className="inline-flex gap-1">
          <Button size="sm" variant="neutral" icon="edit" onClick={() => setDialog({ kind: 'form', boat: b })}>
            {t('Edit')}
          </Button>
          <Button size="sm" variant="neutral" icon="event_seat" to={`${ROUTES.adminBoatSeats}?boatId=${b.id}`}>
            {t('Seats')}
          </Button>
          <ActiveToggle
            active={b.isActive}
            busy={activate.busy}
            onDeactivate={() => setDialog({ kind: 'deactivate', boat: b })}
            onActivate={async () => {
              if (await activate.run(() => setBoatActive(b.id, true).then(() => true))) reload()
            }}
          />
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
        title="Boats"
        description="The boats of the fleet and their number of seats."
        actions={
          <Button size="sm" icon="add" onClick={() => setDialog({ kind: 'form' })}>
            {t('Add boat')}
          </Button>
        }
      />
      <FilterBar>
        <TextField
          label="Search"
          placeholder={t('Search by name or code')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <FilterChips label="Status" value={status} onChange={setStatus} options={[
          { value: '', label: 'All statuses' }, { value: 'active', label: 'Active only' }, { value: 'inactive', label: 'Inactive only' },
        ]} />
      </FilterBar>
      <DataTable paginate columns={columns} rows={filteredBoats} rowKey={(b) => b.id} empty="No boats match this filter." />

      {dialog?.kind === 'form' && (
        <Modal title={dialog.boat ? 'Edit boat' : 'Add boat'} onClose={() => setDialog(null)}>
          <BoatForm boat={dialog.boat} onDone={closeAndReload} onCancel={() => setDialog(null)} />
        </Modal>
      )}

      {dialog?.kind === 'deactivate' && (
        <ConfirmModal
          title="Deactivate boat"
          message={t('Deactivate {name}? Trips already assigned to this boat are not changed; assign another boat on the Trips page if it can no longer sail.', { name: dialog.boat.name })}
          confirmLabel="Deactivate"
          action={() => setBoatActive(dialog.boat.id, false)}
          onClose={() => setDialog(null)}
          onDone={closeAndReload}
        />
      )}
    </>
  )
}
