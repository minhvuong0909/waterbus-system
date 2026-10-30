import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatClock, fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import type { TripStopDto } from '../../trips/types'
import { useSubmit } from '../hooks/useAdmin'
import { setCheckInWindow } from '../services/checkInService'

/** A stop that has left or was skipped keeps its window: the backend refuses to change it. */
const LOCKED_STATUSES = ['Departed', 'Skipped']

/** One stop of the trip: its planned departure and the check-in window staff work in. */
export default function CheckInWindowRow({ stop }: { stop: TripStopDto }) {
  const [openAt, setOpenAt] = useState(stop.checkInOpenAt ? toVietnamDateTimeInput(stop.checkInOpenAt) : '')
  const [closeAt, setCloseAt] = useState(stop.checkInCloseAt ? toVietnamDateTimeInput(stop.checkInCloseAt) : '')
  const [invalid, setInvalid] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  const { run, busy, error } = useSubmit()
  const locked = LOCKED_STATUSES.includes(stop.status)
  const departure = stop.estimatedDepartureTime ?? stop.estimatedArrivalTime

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaved(false)
    if (!openAt || !closeAt) return setInvalid(t('Enter both times.'))
    if (closeAt <= openAt) return setInvalid(t('Check-in must close after it opens.'))
    setInvalid(null)
    const done = await run(() => setCheckInWindow(stop.callId, fromVietnamDateTimeInput(openAt), fromVietnamDateTimeInput(closeAt)))
    if (done) setSaved(true)
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
      <div className="md:col-span-3">
        <TextField label="Opens" type="datetime-local" value={openAt} disabled={locked} onChange={(e) => setOpenAt(e.target.value)} />
      </div>
      <div className="md:col-span-3">
        <TextField label="Closes" type="datetime-local" value={closeAt} disabled={locked} onChange={(e) => setCloseAt(e.target.value)} />
      </div>
      <div className="md:col-span-3">
        <Button type="submit" size="sm" icon="save" disabled={locked || busy} className="w-full">
          {busy ? t('Saving…') : t('Save')}
        </Button>
      </div>
      {(invalid || error || saved) && (
        <div className="md:col-span-12">
          {invalid || error ? <Notice tone="error">{invalid ?? error}</Notice> : <Notice tone="success">{t('Check-in window saved.')}</Notice>}
        </div>
      )}
    </form>
  )
}
