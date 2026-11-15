import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, SelectField, TextField, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import { codeTaken, isCode, parseDecimal, parseWhole } from '../../../utils/inputRules'
import { useSubmit } from '../../../hooks'
import { createRoute, updateRoute } from '../services/routeService'
import { stopsError } from '../utils/routeStops'
import type { AdminStation, RouteDetail, RouteInput, RouteStopInput, ServiceType } from '../types'
import FormActions from './FormActions'
import RouteStopsEditor from './RouteStopsEditor'

const EMPTY_STOP: RouteStopInput = { stationId: '', canBoard: true, canAlight: true }

/**
 * Thêm hoặc sửa một tuyến. Mã cố định sau khi tạo. Tuyến đã được lịch chạy hoặc chuyến dùng (`structureLocked`) giữ
 * nguyên loại dịch vụ và bến dừng; chỉ đổi được tên, thời gian và quãng đường.
 */
export default function RouteForm({
  route,
  others,
  stations,
  onDone,
  onCancel,
}: {
  route?: RouteDetail
  /** Các tuyến khác, để chặn mã trùng. */
  others: { code: string }[]
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
      // Khách lên/xuống ở mọi bến (vé một giá), nên không có lựa chọn riêng từng bến.
      route?.stops.map(({ stationId }) => ({ stationId, canBoard: true, canAlight: true })) ?? [
        EMPTY_STOP,
        EMPTY_STOP,
      ],
  )
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const locked = route?.structureLocked ?? false
  // `stations` gồm cả bến đã ngừng. Chỉ cho chọn bến đang hoạt động (backend từ chối bến đã ngừng); bến tuyến đang
  // ghé sẵn vẫn được giữ để hiển thị đúng khi sửa tuyến.
  const stationOptions = [
    ...stations.filter((station) => station.isActive),
    ...(route?.stops ?? [])
      .filter((stop) => !stations.some((station) => station.id === stop.stationId && station.isActive))
      .map((stop) => ({ id: stop.stationId, name: stop.stationName })),
  ]
  const duration = parseWhole(form.duration, 1, 1440)
  const distance = parseDecimal(form.distance, 0, 1000, 2)
  const errors = {
    code: !form.code.trim()
      ? t('Code is required.')
      : !isCode(form.code)
        ? t('Use letters, numbers, dots, underscores or hyphens.')
        : !route && codeTaken(form.code, others)
          ? t('This code is already used.')
          : undefined,
    name: form.name.trim() ? undefined : t('Name is required.'),
    duration: duration !== null ? undefined : t('Enter the minutes from first to last stop.'),
    distance: distance !== null ? undefined : t('Enter the distance in kilometres.'),
    stops: stopsError(stops, form.serviceType),
  }
  // Tên gợi ý theo bến đầu và bến cuối, để tên không lệch với bến dừng.
  const nameOf = (stationId: string) => stationOptions.find((station) => station.id === stationId)?.name
  const first = nameOf(stops[0]?.stationId ?? '')
  const last = nameOf(stops[stops.length - 1]?.stationId ?? '')
  const suggestedName = first && last ? `${first} - ${last}` : undefined
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
      estimatedDurationMinutes: duration as number,
      distanceKm: distance as number,
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
          required
          value={form.code}
          onChange={set('code')}
          error={shown.code}
          disabled={Boolean(route)}
          maxLength={20}
          placeholder="RT02"
        />
        <SelectField label="Service" required value={form.serviceType} onChange={set('serviceType')} disabled={locked}>
          <option value="Regular">{t('Regular')}</option>
          <option value="Sightseeing">{t('Sightseeing')}</option>
        </SelectField>
      </div>
      <TextField label="Name" required value={form.name} onChange={set('name')} error={shown.name} maxLength={150} />
      {suggestedName && form.name.trim() === '' && (
        <Button size="sm" variant="text" className="-mt-1" onClick={() => setForm((f) => ({ ...f, name: suggestedName }))}>
          {t('Use suggested name: {name}', { name: suggestedName })}
        </Button>
      )}
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Duration (minutes)" required inputMode="numeric" value={form.duration} onChange={set('duration')} error={shown.duration} />
        <TextField label="Distance (km)" required inputMode="decimal" value={form.distance} onChange={set('distance')} error={shown.distance} />
      </div>

      <RouteStopsEditor stops={stops} stations={stationOptions} serviceType={form.serviceType} locked={locked} onChange={setStops} />
      {shown.stops && (
        <p role="alert" className="text-2xs text-coral-glow">
          {shown.stops}
        </p>
      )}
      {locked && (
        <Notice tone="info">
          {t('Schedules or trips already use this route, so its service and stops are fixed. Add a new route to change them.')}
        </Notice>
      )}
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
