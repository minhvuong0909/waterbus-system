import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ErrorState, Notice, Pagination, Panel, SelectField, TextField } from '../../../components/ui'
import { useFetch, useNow, useSubmit } from '../../../hooks'
import { ApiError } from '../../../services/apiClient'
import { t } from '../../../i18n'
import { formatDateTime, fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import type { TripStopDto } from '../../trips/types'
import { assignScanner, listScannerDevices, listStopAssignments, revokeScannerAssignment } from '../services/scannerAssignmentService'
import type { AssignScannerRequest, ScannerAssignment } from '../services/scannerAssignmentService'

export default function ScannerAssignmentsPanel({ stops, routeName, stopId, onStopChange }: {
  stops: TripStopDto[]
  routeName: string
  stopId: string
  onStopChange: (callId: string) => void
}) {
  const stop = stops.find((item) => item.callId === stopId)
  return (
    <Panel title="Primary Offline scanner" className="mt-4 space-y-4" id="scanner-assignments">
      <SelectField label="Boarding stop" value={stopId} onChange={(event) => onStopChange(event.target.value)}>
        <option value="">{t('Choose a boarding stop')}</option>
        {stops.map((item) => <option key={item.callId} value={item.callId}>{item.stationName}</option>)}
      </SelectField>
      {stop && <StopPrimary key={stop.callId} stop={stop} routeName={routeName} />}
    </Panel>
  )
}

type Confirmation = { request: AssignScannerRequest; code: string } | { revoke: ScannerAssignment }

function StopPrimary({ stop, routeName }: { stop: TripStopDto; routeName: string }) {
  const now = useNow(30_000)
  const devices = useFetch(() => listScannerDevices(stop.stationId), [stop.stationId])
  const [deviceId, setDeviceId] = useState('')
  const [fromInput, setFrom] = useState<string>()
  const [untilInput, setUntil] = useState<string>()
  const from = fromInput ?? (stop.checkInOpenAt ? toVietnamDateTimeInput(stop.checkInOpenAt) : '')
  const until = untilInput ?? (stop.checkInCloseAt ? toVietnamDateTimeInput(stop.checkInCloseAt) : '')
  const [page, setPage] = useState(1)
  const assignments = useFetch(() => listStopAssignments(stop.callId, page), [page])
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)
  const [validation, setValidation] = useState('')
  const [saved, setSaved] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const submitting = useRef(false)
  const { run, busy, error } = useSubmit()
  const eligible = devices.data?.filter((device) => device.stationId === stop.stationId && device.status === 'Active') ?? []
  const selected = eligible.find((device) => device.id === deviceId)

  function review(event: FormEvent) {
    event.preventDefault()
    if (!selected || busy || confirmation) return
    const start = Date.parse(from + ':00+07:00')
    const end = Date.parse(until + ':00+07:00')
    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= Math.max(start, now)) {
      setValidation(t('Enter a start time and an end time after the start and current time.'))
      return
    }
    setValidation('')
    setConfirmation({ code: selected.deviceCode, request: {
      tripStopCallId: stop.callId, deviceId: selected.id,
      activeFrom: fromVietnamDateTimeInput(from), activeUntil: fromVietnamDateTimeInput(until),
    } })
  }

  async function confirm() {
    if (!confirmation || busy || submitting.current) return
    submitting.current = true
    setUncertain(false)
    try {
      await run(async () => {
        try {
          if ('request' in confirmation) {
            await assignScanner(confirmation.request)
            setSaved(t('Primary Offline scanner assigned.'))
          } else {
            await revokeScannerAssignment(confirmation.revoke.assignmentId)
            setSaved(t('Assignment revoked. Issued offline grants keep their slot until expiry.'))
          }
          if (page === 1) assignments.retry()
          else setPage(1)
        } catch (failure) {
          if (!(failure instanceof ApiError) || failure.status === 0 || failure.status >= 500) {
            setUncertain(true)
            assignments.retry()
          }
          throw failure
        }
      })
    } finally {
      setConfirmation(null)
      submitting.current = false
    }
  }

  return (
    <>
      <p className="text-sm text-on-surface-variant">{routeName} · {stop.stationName}</p>
      {devices.error && <ErrorState compact onRetry={devices.retry} />}
      <form onSubmit={review} className="space-y-3">
        <fieldset disabled={busy || !!confirmation} className="grid gap-3 md:grid-cols-3">
          <SelectField label="Scanner device" value={selected?.id ?? ''} onChange={(event) => setDeviceId(event.target.value)} required>
            <option value="">{t('Choose a paired scanner')}</option>
            {eligible.map((item) => <option key={item.id} value={item.id}>{item.deviceCode} {item.label}</option>)}
          </SelectField>
          <TextField label="Active from" type="datetime-local" value={from} onChange={(event) => setFrom(event.target.value)} required />
          <TextField label="Active until" type="datetime-local" value={until} onChange={(event) => setUntil(event.target.value)}
            hint="Vietnam time (UTC+07:00)" required />
        </fieldset>
        <Button type="submit" disabled={busy || !selected || !!confirmation}>{t('Review assignment')}</Button>
      </form>
      {confirmation && <div className="space-y-2">
        <p>{'request' in confirmation
          ? t('Assign {code} at {station} from {from} to {until}?', { code: confirmation.code, station: stop.stationName, from: formatDateTime(confirmation.request.activeFrom), until: formatDateTime(confirmation.request.activeUntil) })
          : t('Revoke {code} at {station}?', { code: confirmation.revoke.deviceCode, station: stop.stationName })}</p>
        <div className="flex gap-2">
          <Button disabled={busy} onClick={confirm}>{'request' in confirmation ? t('Confirm assignment') : t('Confirm revoke')}</Button>
          <Button variant="neutral" disabled={busy} onClick={() => setConfirmation(null)}>{t('Cancel')}</Button>
        </div>
      </div>}
      {(validation || error) && <Notice tone="error">{validation || error}</Notice>}
      {uncertain && <Notice tone="error">{t('The server may have saved the change. Check the refreshed assignments before trying again.')}</Notice>}
      {saved && <Notice tone="success">{saved}</Notice>}
      {assignments.error && <ErrorState compact onRetry={assignments.retry} />}
      {assignments.data && <section className="space-y-3">
        <h3 className="font-semibold">{t('Current and upcoming assignments')}</h3>
        {assignments.data.items.length === 0 && <p>{t('No Primary Offline scanner assigned.')}</p>}
        <ul className="divide-y divide-outline-variant/30">
          {assignments.data.items.map((item) => <li key={item.assignmentId} className="flex flex-wrap items-center justify-between gap-3 py-3">
            <span>{item.deviceCode} · {formatDateTime(item.activeFrom)} – {item.activeUntil ? formatDateTime(item.activeUntil) : '—'}</span>
            <Button variant="danger" size="sm" disabled={busy || !!confirmation}
              onClick={() => setConfirmation({ revoke: item })}>{t('Revoke assignment')}</Button>
          </li>)}
        </ul>
        <Pagination pageNumber={page} totalPages={assignments.data.totalPages} onChange={setPage} busy={busy} />
      </section>}
    </>
  )
}
