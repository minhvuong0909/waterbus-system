import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { toVietnamDate } from '../../../utils/time'
import { useSubmit } from '../../../hooks'
import { generateTrips } from '../services/tripPlanningService'
import { EMPTY_TIMING, timingErrors, toTimingInput } from '../utils/tripTiming'
import type { GenerateTripsResult, ScheduleListItem } from '../types'
import FormActions from './FormActions'
import TripTimingFields from './TripTimingFields'

/** Số ngày tối đa backend tạo chuyến trong một lần gửi. */
const MAX_DAYS = 31
const DAY_MS = 86_400_000

const daysBetween = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / DAY_MS)

function Result({ result, onClose }: { result: GenerateTripsResult; onClose: () => void }) {
  const skipped = [
    { count: result.skippedExisting, text: '{count} days already had this trip' },
    { count: result.skippedNotOperatingDay, text: '{count} days are not operating days' },
    { count: result.skippedPast, text: '{count} departures were already in the past' },
  ].filter((line) => line.count > 0)

  return (
    <div>
      <p className="font-display text-2xl font-bold text-deep-river">
        {result.created.length === 1
          ? t('1 trip created')
          : t('{count} trips created', { count: result.created.length })}
      </p>
      {result.created.length > 0 && (
        <p className="mt-1 text-sm text-on-surface-variant">
          {t('Assign a boat to each trip on the Trips page; a trip without a boat is not on sale.')}
        </p>
      )}
      {skipped.length > 0 && (
        <ul className="mt-2 space-y-1 text-sm text-on-surface-variant">
          {skipped.map((line) => (
            <li key={line.text}>{t(line.text, { count: line.count })}</li>
          ))}
        </ul>
      )}
      <div className="mt-4 flex justify-end">
        <Button size="sm" onClick={onClose}>
          {t('Done')}
        </Button>
      </div>
    </div>
  )
}

/**
 * Tạo các chuyến của một lịch chạy cho một khoảng ngày (theo ngày Việt Nam). Chạy lại vẫn an toàn: ngày đã có
 * chuyến được bỏ qua. Chuyến mới chưa có tàu (BR-SCH-04).
 */
export default function GenerateTripsForm({
  schedule,
  onClose,
}: {
  schedule: ScheduleListItem
  onClose: () => void
}) {
  const today = toVietnamDate(new Date())
  // Đề xuất một tuần bắt đầu từ hôm nay.
  const [dates, setDates] = useState(() => ({
    fromDate: today,
    toDate: new Date(Date.parse(today) + 6 * DAY_MS).toISOString().slice(0, 10),
  }))
  const [timing, setTiming] = useState(EMPTY_TIMING)
  const [showErrors, setShowErrors] = useState(false)
  const [result, setResult] = useState<GenerateTripsResult>()
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof dates) => (e: { target: { value: string } }) =>
    setDates((d) => ({ ...d, [key]: e.target.value }))

  if (result) return <Result result={result} onClose={onClose} />

  const span = dates.fromDate && dates.toDate ? daysBetween(dates.fromDate, dates.toDate) : -1
  const errors = {
    fromDate: dates.fromDate ? undefined : t('Choose the first day.'),
    toDate:
      span < 0
        ? t('The last day cannot be before the first day.')
        : span >= MAX_DAYS
          ? t('Choose at most {max} days at a time.', { max: MAX_DAYS })
          : undefined,
    ...timingErrors(timing),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const created = await run(() =>
      generateTrips({ scheduleId: schedule.id, ...dates, ...toTimingInput(timing) }),
    )
    if (created) setResult(created)
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <p className="text-sm text-on-surface-variant">
        {t('{route}, departing {time}.', {
          route: schedule.routeName,
          time: schedule.departureTime.slice(0, 5),
        })}
      </p>
      <div className="grid grid-cols-2 gap-3">
        <TextField label="First day" type="date" min={today} value={dates.fromDate} onChange={set('fromDate')} error={shown.fromDate} />
        <TextField label="Last day" type="date" min={dates.fromDate} value={dates.toDate} onChange={set('toDate')} error={shown.toDate} />
      </div>
      <TripTimingFields timing={timing} errors={shown} onChange={setTiming} />
      <FormActions error={error} busy={busy} submitLabel="Create trips" onCancel={onClose} />
    </form>
  )
}
