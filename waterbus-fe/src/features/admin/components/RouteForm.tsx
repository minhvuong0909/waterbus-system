import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../hooks/useAdmin'
import { createRoute, updateRoute } from '../services/routeService'
import type { AdminStation, RouteDetail, RouteInput, RouteStopInput, ServiceType } from '../types'
import FormActions from './FormActions'
import RouteStopsEditor from './RouteStopsEditor'

const EMPTY_STOP: RouteStopInput = { stationId: '', canBoard: true, canAlight: true }

/**
 * Adds a route or edits one. The code is fixed once created. A route already used by a schedule
 * or trip (`structureLocked`) keeps its service type and stops; only its name, duration and
 * distance can change.
 */
export default function RouteForm({
  route,
  stations,
  onDone,
  onCancel,
}: {
  route?: RouteDetail
  stations: AdminStation[]
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    code: route?.code ?? '',
    name: route?.name ?? '',
    serviceType: route?.serviceType ?? ('Regular' as ServiceType),
    duration: route ? String(route.estimatedDurationMinutes) : '',
    distance: route ? String(route.distanceKm) : '',
  })
  const [stops, setStops] = useState<RouteStopInput[]>(
    () =>
      route?.stops.map(({ stationId, canBoard, canAlight }) => ({ stationId, canBoard, canAlight })) ?? [
        EMPTY_STOP,
        EMPTY_STOP,
      ],
  )
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const locked = route?.structureLocked ?? false
  // `stations` only has the active ones; a station the route already calls at stays selectable.
  const stationOptions = [
    ...stations,
    ...(route?.stops ?? [])
      .filter((stop) => !stations.some((station) => station.id === stop.stationId))
      .map((stop) => ({ id: stop.stationId, name: stop.stationName })),
  ]
  const duration = Number(form.duration)
  const distance = Number(form.distance)
  const errors = {
    code: form.code.trim() ? undefined : t('Code is required.'),
    name: form.name.trim() ? undefined : t('Name is required.'),
    duration:
      Number.isInteger(duration) && duration >= 1 && duration <= 1440
        ? undefined
        : t('Enter the minutes from first to last stop.'),
    distance:
      form.distance.trim() !== '' && distance >= 0 && distance <= 1000
        ? undefined
        : t('Enter the distance in kilometres.'),
    stops: stops.every((stop) => stop.stationId) ? undefined : t('Choose a station for every stop.'),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const input: RouteInput = {
      code: form.code.trim().toUpperCase(),
      name: form.name.trim(),
      serviceType: form.serviceType,
      estimatedDurationMinutes: duration,
      distanceKm: distance,
      stops,
    }
    const saved = await run(async () => {
      if (route) await updateRoute(route, input)
      else await createRoute(input)
      return true
    })
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField
          label="Code"
          value={form.code}
          onChange={set('code')}
          error={shown.code}
          disabled={Boolean(route)}
          maxLength={20}
          placeholder="RT02"
        />
        <SelectField label="Service" value={form.serviceType} onChange={set('serviceType')} disabled={locked}>
          <option value="Regular">{t('Regular')}</option>
          <option value="Sightseeing">{t('Sightseeing')}</option>
        </SelectField>
      </div>
      <TextField label="Name" value={form.name} onChange={set('name')} error={shown.name} maxLength={150} />
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Duration (minutes)" inputMode="numeric" value={form.duration} onChange={set('duration')} error={shown.duration} />
        <TextField label="Distance (km)" inputMode="decimal" value={form.distance} onChange={set('distance')} error={shown.distance} />
      </div>

      <RouteStopsEditor stops={stops} stations={stationOptions} locked={locked} onChange={setStops} />
      {shown.stops && (
        <p role="alert" className="text-2xs text-coral-glow">
          {shown.stops}
        </p>
      )}
      {locked && (
        <p className="rounded-lg bg-mist px-3 py-2 text-sm text-on-surface-variant">
          {t('Schedules or trips already use this route, so its service and stops are fixed. Add a new route to change them.')}
        </p>
      )}
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
