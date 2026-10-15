import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  ActionButton,
  DataTable,
  Modal,
  Notice,
  PageHeader,
  SelectField,
  TextField,
} from '../../features/admin/components/AdminUi'
import type { Column } from '../../features/admin/components/AdminUi'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  createSchedule,
  generateTrips,
  getBoats,
  getRoutes,
  getSchedules,
  setScheduleActive,
} from '../../features/admin/services/adminService'
import type { AdminBoat, AdminRoute, AdminSchedule, AdminTripType } from '../../features/admin/types'
import { WEEKDAYS } from '../../features/admin/utils'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import { toIsoDate } from '../../utils/time'

type Dialog = { kind: 'create' } | { kind: 'generate'; schedule: AdminSchedule } | null

const TRIP_TYPES: { id: AdminTripType; label: string }[] = [
  { id: 'Commuter', label: 'Regular' },
  { id: 'Sightseeing', label: 'Sightseeing' },
]

function DayPicker({ value, onChange }: { value: number[]; onChange: (days: number[]) => void }) {
  const toggle = (day: number) =>
    onChange(value.includes(day) ? value.filter((d) => d !== day) : [...value, day].sort())
  return (
    <div className="flex flex-wrap gap-1.5" role="group" aria-label={t('Days of the week')}>
      {WEEKDAYS.map(({ day, label }) => (
        <button
          key={day}
          type="button"
          aria-pressed={value.includes(day)}
          onClick={() => toggle(day)}
          className={`rounded-full px-3 py-1 text-xs font-semibold ${
            value.includes(day) ? 'bg-teal-flow text-on-primary' : 'border border-outline-variant text-on-surface-variant'
          }`}
        >
          {t(label)}
        </button>
      ))}
    </div>
  )
}

function ScheduleForm({ routes, onDone, onCancel }: { routes: AdminRoute[]; onDone: () => void; onCancel: () => void }) {
  const [routeId, setRouteId] = useState(routes[0]?.id ?? '')
  const [time, setTime] = useState('07:00')
  const [days, setDays] = useState<number[]>([1, 2, 3, 4, 5, 6, 7])
  const [tripType, setTripType] = useState<AdminTripType>('Commuter')
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!routeId || !time || days.length === 0) {
      setShowErrors(true)
      return
    }
    const saved = await run(() => createSchedule({ routeId, departureTime: time, daysOfWeek: days, tripType }))
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <SelectField label="Route" value={routeId} onChange={(e) => setRouteId(e.target.value)}>
        {routes.map((r) => (
          <option key={r.id} value={r.id}>
            {r.code} • {r.name}
          </option>
        ))}
      </SelectField>
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Departure time" type="time" value={time} onChange={(e) => setTime(e.target.value)} error={showErrors && !time ? t('Pick a time.') : undefined} />
        <SelectField label="Trip type" value={tripType} onChange={(e) => setTripType(e.target.value as AdminTripType)}>
          {TRIP_TYPES.map((type) => (
            <option key={type.id} value={type.id}>
              {t(type.label)}
            </option>
          ))}
        </SelectField>
      </div>
      <div>
        <div className="mb-1 text-xs font-semibold text-on-surface-variant">{t('Runs on')}</div>
        <DayPicker value={days} onChange={setDays} />
        {showErrors && days.length === 0 && (
          <p role="alert" className="mt-1 text-[11px] text-coral-glow">
            {t('Pick at least one day.')}
          </p>
        )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Cancel')}
        </ActionButton>
        <ActionButton type="submit" disabled={busy}>
          {busy ? t('Saving…') : t('Save schedule')}
        </ActionButton>
      </div>
    </form>
  )
}

function GenerateForm({ schedule, boats, onCancel, onDone }: { schedule: AdminSchedule; boats: AdminBoat[]; onCancel: () => void; onDone: () => void }) {
  const [boatId, setBoatId] = useState(boats[0]?.id ?? '')
  // Default range: the next seven days, starting today.
  const [fromDate, setFromDate] = useState(() => toIsoDate(new Date()))
  const [toDate, setToDate] = useState(() => {
    const end = new Date()
    end.setDate(end.getDate() + 6)
    return toIsoDate(end)
  })
  const [result, setResult] = useState<{ created: number; skipped: number } | null>(null)
  const { run, busy, error } = useSubmit()

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const done = await run(() => generateTrips({ scheduleId: schedule.id, boatId, fromDate, toDate }))
    if (done) {
      setResult(done)
      onDone()
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <p className="text-sm text-on-surface-variant">
        {t('Creates a trip for every matching day at {time}. Days that already have this trip are skipped.', { time: schedule.departureTime })}
      </p>
      <SelectField label="Boat" hint="A schedule has no boat of its own: choose the one that runs these trips." value={boatId} onChange={(e) => setBoatId(e.target.value)}>
        {boats.map((b) => (
          <option key={b.id} value={b.id}>
            {b.code} • {b.name} ({b.totalSeats})
          </option>
        ))}
      </SelectField>
      <div className="grid grid-cols-2 gap-3">
        <TextField label="From" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        <TextField label="To" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
      </div>
      {result && (
        <Notice tone="success">
          {t('Created {created} trips, skipped {skipped} that already existed.', result)}
        </Notice>
      )}
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {result ? t('Close') : t('Cancel')}
        </ActionButton>
        <ActionButton type="submit" disabled={busy || !boatId || !fromDate || !toDate}>
          {busy ? t('Generating…') : t('Generate trips')}
        </ActionButton>
      </div>
    </form>
  )
}

export default function SchedulesPage() {
  useDocumentTitle('Schedules')
  const [version, reload] = useReload()
  const schedules = useFetch(getSchedules, [version])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  const [dialog, setDialog] = useState<Dialog>(null)
  const toggle = useSubmit()
  const state = combineFetches(schedules, routes, boats)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !schedules.data || !routes.data || !boats.data) return <PageLoader />

  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'
  const columns: Column<AdminSchedule>[] = [
    { header: 'Route', render: (s) => routeName(s.routeId) },
    { header: 'Departure', render: (s) => <span className="font-mono font-semibold">{s.departureTime}</span> },
    {
      header: 'Runs on',
      render: (s) => WEEKDAYS.filter((d) => s.daysOfWeek.includes(d.day)).map((d) => t(d.label)).join(', '),
    },
    { header: 'Type', render: (s) => t(s.tripType === 'Commuter' ? 'Regular' : 'Sightseeing') },
    {
      header: 'Status',
      render: (s) => (
        <span className={s.isActive ? 'font-semibold text-teal-flow' : 'text-on-surface-variant'}>
          {s.isActive ? t('Active') : t('Paused')}
        </span>
      ),
    },
    {
      header: 'Actions',
      className: 'text-right',
      render: (s) => (
        <span className="inline-flex gap-1">
          <ActionButton tone="secondary" icon="auto_mode" disabled={!s.isActive} onClick={() => setDialog({ kind: 'generate', schedule: s })}>
            {t('Generate trips')}
          </ActionButton>
          <ActionButton
            tone="secondary"
            disabled={toggle.busy}
            onClick={async () => {
              await toggle.run(() => setScheduleActive(s.id, !s.isActive))
              reload()
            }}
          >
            {s.isActive ? t('Pause') : t('Resume')}
          </ActionButton>
        </span>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Schedules"
        description="The fixed weekly timetable. Generate it into real trips for a range of days."
        actions={
          <ActionButton icon="add" onClick={() => setDialog({ kind: 'create' })}>
            {t('Add schedule')}
          </ActionButton>
        }
      />
      {toggle.error && <div className="mb-3"><Notice tone="error">{toggle.error}</Notice></div>}
      <DataTable columns={columns} rows={schedules.data} rowKey={(s) => s.id} />

      {dialog?.kind === 'create' && (
        <Modal title="Add schedule" onClose={() => setDialog(null)}>
          <ScheduleForm
            routes={routes.data}
            onCancel={() => setDialog(null)}
            onDone={() => {
              setDialog(null)
              reload()
            }}
          />
        </Modal>
      )}
      {dialog?.kind === 'generate' && (
        <Modal title="Generate trips" onClose={() => setDialog(null)}>
          <GenerateForm schedule={dialog.schedule} boats={boats.data} onCancel={() => setDialog(null)} onDone={reload} />
        </Modal>
      )}
    </>
  )
}
