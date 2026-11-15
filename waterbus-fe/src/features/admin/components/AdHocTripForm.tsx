import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { hasSaneYear } from '../../../utils/inputRules'
import { fromVietnamDateTimeInput } from '../../../utils/time'
import { useFetch, useSubmit } from '../../../hooks'
import { buildStopRows, stopRowsError, toStopInputs } from '../utils/scheduleStops'
import type { StopEdit } from '../utils/scheduleStops'
import { getRoute } from '../services/routeService'
import { createAdHocTrip } from '../services/tripPlanningService'
import { EMPTY_TIMING, timingErrors, toTimingInput } from '../utils/tripTiming'
import type { RouteListItem, ScheduleDirection } from '../types'
import FormActions from './FormActions'
import ScheduleStopsEditor from './ScheduleStopsEditor'
import TripTimingFields from './TripTimingFields'

/**
 * Một chuyến ngoài lịch chạy (BR-SCH-10): tuyến, các bến thực sự phục vụ và giờ khởi hành. Loại chuyến theo loại
 * dịch vụ của tuyến (BR-ROUTE-03). Tàu được gán sau.
 */
export default function AdHocTripForm({
  routes,
  onDone,
  onCancel,
}: {
  routes: RouteListItem[]
  /** Gọi kèm giờ khởi hành (thời điểm ISO) của chuyến vừa tạo. */
  onDone: (departureTime: string) => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    routeId: routes[0]?.id ?? '',
    direction: 'Outbound' as ScheduleDirection,
    departure: '',
  })
  const [edits, setEdits] = useState<Record<string, StopEdit>>({})
  const [timing, setTiming] = useState(EMPTY_TIMING)
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const route = useFetch(() => getRoute(form.routeId), [form.routeId])
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  if (route.error) return <ErrorState compact onRetry={route.retry} />

  const rows = buildStopRows(route.data?.id === form.routeId ? route.data.stops : [], form.direction, edits)
  const errors = {
    routeId: form.routeId ? undefined : t('Choose a route.'),
    departure: hasSaneYear(form.departure) ? undefined : t('Choose the departure time.'),
    stops: stopRowsError(rows),
    ...timingErrors(timing),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!route.data || Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const created = await run(() =>
      createAdHocTrip({
        routeId: form.routeId,
        tripType: route.data?.serviceType === 'Sightseeing' ? 'Sightseeing' : 'Commuter',
        departureTime: fromVietnamDateTimeInput(form.departure),
        stops: toStopInputs(rows),
        ...toTimingInput(timing),
      }),
    )
    if (created) onDone(created.departureTime)
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <SelectField
          label="Route"
          required
          value={form.routeId}
          onChange={(e) => {
            set('routeId')(e)
            setEdits({})
          }}
          error={shown.routeId}
        >
          {routes.map((r) => (
            <option key={r.id} value={r.id}>{`${r.code} · ${r.name}`}</option>
          ))}
        </SelectField>
        <SelectField label="Direction" required value={form.direction} onChange={set('direction')}>
          <option value="Outbound">{t('Outbound')}</option>
          <option value="Inbound">{t('Inbound')}</option>
        </SelectField>
      </div>
      <TextField
        label="Departs"
        required
        hint="Vietnam time."
        type="datetime-local"
        value={form.departure}
        onChange={set('departure')}
        error={shown.departure}
      />

      <ScheduleStopsEditor
        rows={rows}
        locked={false}
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
      <TripTimingFields timing={timing} errors={shown} onChange={setTiming} />
      <FormActions error={error} busy={busy} submitLabel="Create trip" disabled={!route.data} onCancel={onCancel} />
    </form>
  )
}
