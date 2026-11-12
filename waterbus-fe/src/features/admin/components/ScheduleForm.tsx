import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, SelectField, TextField } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { toVietnamDate } from '../../../utils/time'
import { useSubmit } from '../hooks/useAdmin'
import { formatDays, parseDays } from '../scheduleDays'
import { buildStopRows, stopRowsError, toStopInputs } from '../scheduleStops'
import type { StopEdit } from '../scheduleStops'
import { getRoute } from '../services/routeService'
import { createSchedule, updateSchedule } from '../services/scheduleService'
import type { RouteListItem, ScheduleDetail, ScheduleDirection, ScheduleInput } from '../types'
import DayPicker from './DayPicker'
import FormActions from './FormActions'
import ScheduleStopsEditor from './ScheduleStopsEditor'

/**
 * Adds a schedule or edits one. A schedule belongs to one route and one direction for good; its
 * trip type follows the service of the route. Once it has produced trips (`structureLocked`) its
 * stops are fixed (BR-SCH-06) and only the times, days and validity can change.
 */
export default function ScheduleForm({
  schedule,
  routes,
  onDone,
  onCancel,
}: {
  schedule?: ScheduleDetail
  routes: RouteListItem[]
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    routeId: schedule?.routeId ?? routes[0]?.id ?? '',
    direction: schedule?.direction ?? ('Outbound' as ScheduleDirection),
    departureTime: schedule?.departureTime.slice(0, 5) ?? '',
    effectiveFrom: schedule?.effectiveFrom ?? toVietnamDate(new Date()),
    effectiveTo: schedule?.effectiveTo ?? '',
  })
  const [days, setDays] = useState(() => (schedule ? parseDays(schedule.daysOfWeek) : [1, 2, 3, 4, 5, 6, 7]))
  // Only the stops the admin touched; untouched ones keep the schedule's values (or the defaults).
  const [edits, setEdits] = useState<Record<string, StopEdit>>({})
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const route = useFetch(() => getRoute(form.routeId), [form.routeId])
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  if (route.error) return <ErrorState compact onRetry={route.retry} />

  const locked = schedule?.structureLocked ?? false
  // Until the chosen route has loaded there are no stops to show.
  const rows = buildStopRows(
    route.data?.id === form.routeId ? route.data.stops : [],
    form.direction,
    edits,
    schedule?.stops,
  )

  const errors = {
    routeId: form.routeId ? undefined : t('Choose a route.'),
    departureTime: form.departureTime ? undefined : t('Choose the departure time.'),
    days: days.length > 0 ? undefined : t('Choose at least one day.'),
    effectiveFrom: form.effectiveFrom ? undefined : t('Choose the first day.'),
    effectiveTo:
      !form.effectiveTo || form.effectiveTo >= form.effectiveFrom
        ? undefined
        : t('The last day cannot be before the first day.'),
    stops: stopRowsError(rows),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!route.data || Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const input: ScheduleInput = {
      routeId: form.routeId,
      tripType: schedule?.tripType ?? (route.data.serviceType === 'Sightseeing' ? 'Sightseeing' : 'Commuter'),
      direction: form.direction,
      departureTime: `${form.departureTime}:00`,
      daysOfWeek: formatDays(days),
      effectiveFrom: form.effectiveFrom,
      effectiveTo: form.effectiveTo || null,
      stops: toStopInputs(rows),
    }
    const saved = await run(async () => {
      if (schedule) await updateSchedule(schedule, input)
      else await createSchedule(input)
      return true
    })
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <SelectField
          label="Route"
          value={form.routeId}
          onChange={(e) => {
            set('routeId')(e)
            setEdits({})
          }}
          disabled={Boolean(schedule)}
          error={shown.routeId}
        >
          {schedule ? (
            <option value={schedule.routeId}>{`${schedule.routeCode} · ${schedule.routeName}`}</option>
          ) : (
            routes.map((r) => (
              <option key={r.id} value={r.id}>{`${r.code} · ${r.name}`}</option>
            ))
          )}
        </SelectField>
        <SelectField label="Direction" value={form.direction} onChange={set('direction')} disabled={Boolean(schedule)}>
          <option value="Outbound">{t('Outbound')}</option>
          <option value="Inbound">{t('Inbound')}</option>
        </SelectField>
      </div>
      <div className="grid grid-cols-3 gap-3">
        <TextField
          label="Departs"
          hint="Vietnam time."
          type="time"
          value={form.departureTime}
          onChange={set('departureTime')}
          error={shown.departureTime}
        />
        <TextField label="First day" type="date" value={form.effectiveFrom} onChange={set('effectiveFrom')} error={shown.effectiveFrom} />
        <TextField label="Last day" hint="Leave empty for no end." type="date" value={form.effectiveTo} onChange={set('effectiveTo')} error={shown.effectiveTo} />
      </div>
      <DayPicker days={days} error={shown.days} onChange={setDays} />

      <ScheduleStopsEditor
        rows={rows}
        locked={locked}
        onChange={(id, patch) =>
          setEdits((current) => {
            const row = rows.find((r) => r.routeStopId === id)
            if (!row) return current
            const { included, arrival, departure } = row
            return { ...current, [id]: { included, arrival, departure, ...patch } }
          })
        }
      />
      {shown.stops && (
        <p role="alert" className="text-2xs text-coral-glow">
          {shown.stops}
        </p>
      )}
      {locked && (
        <p className="rounded-lg bg-mist px-3 py-2 text-sm text-on-surface-variant">
          {t('This schedule has already produced trips, so its stops are fixed. Add a new schedule to change them.')}
        </p>
      )}
      <FormActions error={error} busy={busy} disabled={!route.data} onCancel={onCancel} />
    </form>
  )
}
