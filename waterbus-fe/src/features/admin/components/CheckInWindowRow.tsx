import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatClock, fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import type { TripStopDto } from '../../trips/types'
import { useSubmit } from '../../../hooks'
import type { CheckInWindow } from '../services/checkInService'
import { setCheckInWindow } from '../services/checkInService'

/** Bến đã rời hoặc bị bỏ qua giữ nguyên cửa sổ: backend không cho đổi. */
const LOCKED_STATUSES = ['Departed', 'Skipped']

/** Một bến của chuyến: giờ rời dự kiến và cửa sổ check-in nhân viên làm việc. */
export default function CheckInWindowRow({ stop, onSaved, onAssign }: {
  stop: TripStopDto
  onSaved?: (window: CheckInWindow) => void
  /** Chuyển xuống khung phân công soát vé với bến này đã được chọn. */
  onAssign?: () => void
}) {
  const [openAt, setOpenAt] = useState(stop.checkInOpenAt ? toVietnamDateTimeInput(stop.checkInOpenAt) : '')
  const [closeAt, setCloseAt] = useState(stop.checkInCloseAt ? toVietnamDateTimeInput(stop.checkInCloseAt) : '')
  const [invalid, setInvalid] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const { run, busy, error } = useSubmit()
  const locked = LOCKED_STATUSES.includes(stop.status)
  const departure = stop.estimatedDepartureTime

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaved(false)
    if (!openAt || !closeAt) return setInvalid(t('Enter both times.'))
    if (closeAt <= openAt) return setInvalid(t('Check-in must close after it opens.'))
    // Check-in diễn ra ở khu chờ trước khi tàu rời bến này (BR-BOARD-01/05), nên không được đóng muộn hơn.
    if (departure && Date.parse(fromVietnamDateTimeInput(closeAt)) > Date.parse(departure)) {
      return setInvalid(t('Check-in must close no later than the boat leaves this stop ({time}).', {
        time: formatClock(new Date(departure)),
      }))
    }
    setInvalid(null)
    const done = await run(() => setCheckInWindow(stop.callId, fromVietnamDateTimeInput(openAt), fromVietnamDateTimeInput(closeAt)))
    if (done) {
      setSaved(true)
      onSaved?.(done)
    }
  }

  return (
    <form onSubmit={submit} noValidate className="grid gap-3 py-4 md:grid-cols-12 md:items-end">
      <div className="md:col-span-3">
        <p className="font-semibold text-deep-river">{stop.stationName}</p>
        <p className="text-xs text-on-surface-variant">
          {departure ? t('Departs {time}', { time: formatClock(new Date(departure)) }) : t('No time set')}
          {locked && ` • ${t(stop.status)}`}
        </p>
      </div>
      <div className="md:col-span-2">
        <TextField label="Opens" type="datetime-local" value={openAt} disabled={locked} onChange={(e) => setOpenAt(e.target.value)} />
      </div>
      <div className="md:col-span-2">
        <TextField label="Closes" type="datetime-local" value={closeAt} disabled={locked} onChange={(e) => setCloseAt(e.target.value)} />
      </div>
      <div className="flex flex-wrap items-center gap-2 md:col-span-5">
        <Button type="submit" size="sm" icon="save" disabled={locked || busy}>
          {busy ? t('Saving…') : t('Save')}
        </Button>
        {departure && !locked && (
          <Button size="sm" variant="text" onClick={() => setCloseAt(toVietnamDateTimeInput(departure))}>
            {t('Close at departure')}
          </Button>
        )}
        {onAssign && !locked && (
          <Button size="sm" variant="text" onClick={onAssign}>
            {t('Assign scanners')}
          </Button>
        )}
      </div>
      {(invalid || error || saved) && (
        <div className="md:col-span-12">
          {invalid || error ? <Notice tone="error">{invalid ?? error}</Notice> : <Notice tone="success">{t('Check-in window saved.')}</Notice>}
        </div>
      )}
    </form>
  )
}
