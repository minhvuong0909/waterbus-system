import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ErrorState, Notice, Panel, SelectField, TextField } from '../../../components/ui'
import { useFetch, useSubmit } from '../../../hooks'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { formatDateTime, fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import type { TripStopDto } from '../../trips/types'
import {
  assignScanner, listScannerDevices, listScannerStaff, listStopAssignments, revokeScannerAssignment,
} from '../services/scannerAssignmentService'
import type { AssignScannerRequest, ScannerAssignment, ScannerDevice, ScannerStaff } from '../services/scannerAssignmentService'
import ScannerDeviceRegistration from './ScannerDeviceRegistration'

export default function ScannerAssignmentsPanel({ stops, routeName }: {
  stops: TripStopDto[]
  routeName: string
}) {
  const [stopId, setStopId] = useState('')
  const staff = useFetch(listScannerStaff)
  const devices = useFetch(listScannerDevices)
  const [deviceId, setDeviceId] = useState('')
  const [busy, setBusy] = useState(false)
  const stop = stops.find((item) => item.callId === stopId)

  return (
    <Panel title="Scanner assignments" className="mt-4 space-y-4">
      <SelectField label="Boarding stop" value={stopId} disabled={busy} onChange={(event) => setStopId(event.target.value)}>
        <option value="">{t('Choose a boarding stop')}</option>
        {stops.map((item) => (
          <option key={item.callId} value={item.callId}>
            {item.stationName} · {item.estimatedDepartureTime ? formatDateTime(item.estimatedDepartureTime) : t('No time set')}
          </option>
        ))}
      </SelectField>
      {stops.length === 0 && <p>{t('No boarding stops available for assignment.')}</p>}
      {stop && (
        <>
          <p className="text-sm text-on-surface-variant">
            {routeName} · {stop.stationName}
            {stop.checkInOpenAt && stop.checkInCloseAt &&
              ' · ' + t('Check-in window') + ' ' + formatDateTime(stop.checkInOpenAt) + ' – ' + formatDateTime(stop.checkInCloseAt)}
          </p>
          {staff.error && <ErrorState compact onRetry={staff.retry} />}
          {devices.error && <ErrorState compact onRetry={devices.retry} />}
          {(staff.fetching || devices.fetching) && <p role="status">{t('Loading')}</p>}
          {staff.data?.length === 0 && <p>{t('No active Staff accounts.')}</p>}
          <ScannerDeviceRegistration disabled={busy || devices.fetching} onRegistered={(id) => {
            setDeviceId(id)
            devices.retry()
          }} />
          <StopAssignments key={stop.callId} stop={stop} routeName={routeName}
            staff={staff.data ?? []} devices={devices.data ?? []}
            deviceId={deviceId} onDeviceChange={setDeviceId} onBusyChange={setBusy} />
        </>
      )}
    </Panel>
  )
}

type Confirmation =
  | { kind: 'create'; request: AssignScannerRequest }
  | { kind: 'revoke'; assignment: ScannerAssignment }

function StopAssignments({ stop, routeName, staff, devices, deviceId, onDeviceChange, onBusyChange }: {
  stop: TripStopDto
  routeName: string
  staff: ScannerStaff[]
  devices: ScannerDevice[]
  deviceId: string
  onDeviceChange: (id: string) => void
  onBusyChange: (busy: boolean) => void
}) {
  const [staffId, setStaffId] = useState('')
  const [primary, setPrimary] = useState(false)
  const [nowAtSelection] = useState(() => new Date().toISOString())
  const [fromInput, setFrom] = useState<string>()
  const [untilInput, setUntil] = useState<string>()
  const from = fromInput ?? toVietnamDateTimeInput(stop.checkInOpenAt ?? nowAtSelection)
  const until = untilInput ?? (stop.checkInCloseAt ? toVietnamDateTimeInput(stop.checkInCloseAt) : '')
  const [page, setPage] = useState(1)
  const assignments = useFetch(() => listStopAssignments(stop.callId, page), [page])
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)
  const [success, setSuccess] = useState<{ id: string; summary: string } | null>(null)
  const [validationError, setValidationError] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const submitting = useRef(false)
  const { run, busy, error, clearError } = useSubmit()

  const data = !assignments.fetching && !assignments.error ? assignments.data : undefined
  if (data && page > Math.max(1, data.totalPages)) setPage(Math.max(1, data.totalPages))
  const selectedStaff = staff.find((item) => item.id === staffId)
  const selectedDevice = devices.find((item) => item.id === deviceId && item.isActive)
  const validSelection = Boolean(selectedStaff && selectedDevice)

  function review(event: FormEvent) {
    event.preventDefault()
    if (busy || confirmation || !data || !validSelection) return
    const now = Date.now()
    const validFrom = from && Number.isFinite(Date.parse(from + ':00+07:00'))
    const validUntil = !until || (Number.isFinite(Date.parse(until + ':00+07:00')) &&
      until > from && Date.parse(until + ':00+07:00') > now)
    setValidationError(!validFrom || !validUntil)
    if (!validFrom || !validUntil) return
    clearError()
    setUncertain(false)
    setConfirmation({
      kind: 'create',
      request: {
        tripStopCallId: stop.callId, staffAccountId: staffId, deviceId, isPrimaryOffline: primary,
        activeFrom: fromVietnamDateTimeInput(from), activeUntil: until ? fromVietnamDateTimeInput(until) : null,
      },
    })
  }

  const canConfirm = data && confirmation && (confirmation.kind === 'create'
    ? validSelection
    : data.items.includes(confirmation.assignment))

  async function submit() {
    if (!confirmation || !canConfirm || submitting.current) return
    submitting.current = true
    onBusyChange(true)
    setSuccess(null)
    try {
      await run(async () => {
        try {
          if (confirmation.kind === 'create') {
            const created = await assignScanner(confirmation.request)
            setSuccess({ id: created.assignmentId, summary: confirmationText })
            if (page === 1) assignments.retry()
            else setPage(1)
          } else {
            await revokeScannerAssignment(confirmation.assignment.assignmentId)
            assignments.retry()
          }
        } catch (failure) {
          if (!(failure instanceof ApiError) || failure.status === 0 || failure.status >= 500) {
            setUncertain(true)
            if (page === 1) assignments.retry()
            else setPage(1)
          }
          throw failure
        }
      })
    } finally {
      setConfirmation(null)
      submitting.current = false
      onBusyChange(false)
    }
  }

  const confirmationText = confirmation?.kind === 'create'
    ? [routeName, stop.stationName, selectedStaff?.fullName, selectedDevice?.deviceCode,
      formatDateTime(confirmation.request.activeFrom) + ' – ' +
        (confirmation.request.activeUntil ? formatDateTime(confirmation.request.activeUntil) : t('No end time')),
      confirmation.request.isPrimaryOffline ? t('Primary Offline scanner') : t('Secondary scanner')].join(' · ')
    : confirmation?.kind === 'revoke'
      ? t('Revoke assignment {id} for scanner {code}?', {
        id: confirmation.assignment.assignmentId, code: confirmation.assignment.deviceCode,
      })
      : ''

  return (
    <>
      <form onSubmit={review} className="space-y-4">
        <fieldset disabled={busy || Boolean(confirmation)} className="grid gap-3 md:grid-cols-2">
          <SelectField label="Staff account" value={staffId} onChange={(event) => setStaffId(event.target.value)}>
            <option value="">{t('Choose Staff')}</option>
            {staff.map((item) => (
              <option key={item.id} value={item.id}>
                {[item.fullName, item.staffCode ?? item.email].filter(Boolean).join(' · ')}
              </option>
            ))}
          </SelectField>
          <SelectField label="Scanner device" value={selectedDevice?.id ?? ''} onChange={(event) => onDeviceChange(event.target.value)}
            hint={t('Use the same scanner code in the Staff Scanner app.')}>
            <option value="">{t('Choose a scanner')}</option>
            {devices.filter((item) => item.isActive).map((item) => (
              <option key={item.id} value={item.id}>{[item.deviceCode, item.label].filter(Boolean).join(' · ')}</option>
            ))}
          </SelectField>
          <TextField label="Active from" type="datetime-local" value={from} onChange={(event) => setFrom(event.target.value)}
            hint={t('Vietnam time (UTC+07:00)')} required />
          <TextField label="Active until (optional)" type="datetime-local" value={until}
            onChange={(event) => setUntil(event.target.value)} hint={t('Vietnam time (UTC+07:00)')} />
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={primary} onChange={(event) => setPrimary(event.target.checked)} />
            {t('Primary Offline scanner')}
          </label>
        </fieldset>
        {validationError && <Notice tone="error">{t('Enter a start time and an optional end time after both the start and the current time.')}</Notice>}
        <Button type="submit" disabled={!validSelection || !data || busy || Boolean(confirmation)}>
          {t('Review assignment')}
        </Button>
      </form>
      {confirmation && (
        <div className="space-y-3 rounded-xl border border-outline-variant/40 p-4">
          <p>{confirmationText}</p>
          <div className="flex gap-2">
            <Button variant={confirmation.kind === 'revoke' ? 'danger' : 'dark'} disabled={!canConfirm || busy} onClick={submit}>
              {t(confirmation.kind === 'revoke' ? 'Confirm revoke' : 'Confirm assignment')}
            </Button>
            <Button variant="neutral" disabled={busy} onClick={() => setConfirmation(null)}>{t('Cancel')}</Button>
          </div>
        </div>
      )}
      {error && <Notice tone="error">{error}</Notice>}
      {uncertain && <Notice tone="error">{t('The server may have saved the change. Check the refreshed assignments before trying again.')}</Notice>}
      {success && (
        <Notice tone="success">
          {t('Created assignment {id}.', { id: success.id })}{' '}{success.summary}
        </Notice>
      )}
      <section className="space-y-3 border-t border-outline-variant/40 pt-4">
        <h3 className="font-semibold">{t('Current and upcoming assignments')}</h3>
        {assignments.error && <ErrorState compact onRetry={assignments.retry} />}
        {assignments.fetching && <p role="status">{t('Loading')}</p>}
        {data && data.totalCount === 0 && <p>{t('No current or upcoming assignments at this stop.')}</p>}
        {data && data.items.length > 0 && (
          <ul className="divide-y divide-outline-variant/40">
            {data.items.map((item) => (
              <li key={item.assignmentId} className="flex flex-wrap items-center justify-between gap-3 py-3">
                <div>
                  <p className="font-semibold">{item.staffName || t('Staff account unavailable')} · {item.deviceCode}</p>
                  <p className="text-sm">{formatDateTime(item.activeFrom)} – {item.activeUntil ? formatDateTime(item.activeUntil) : t('No end time')}
                    {' · '}{t(item.isPrimaryOffline ? 'Primary Offline scanner' : 'Secondary scanner')}</p>
                  <p className="break-all font-mono text-xs">{item.assignmentId}</p>
                </div>
                <Button variant="danger" size="sm" disabled={busy || Boolean(confirmation)} onClick={() => {
                  clearError()
                  setUncertain(false)
                  setConfirmation({ kind: 'revoke', assignment: item })
                }}>{t('Revoke assignment')}</Button>
              </li>
            ))}
          </ul>
        )}
        {data && data.totalPages > 1 && (
          <div className="flex items-center gap-2">
            <Button variant="neutral" disabled={page <= 1 || busy || Boolean(confirmation)} onClick={() => setPage(page - 1)}>{t('Previous page')}</Button>
            <span>{page} / {data.totalPages}</span>
            <Button variant="neutral" disabled={page >= data.totalPages || busy || Boolean(confirmation)} onClick={() => setPage(page + 1)}>{t('Next page')}</Button>
          </div>
        )}
      </section>
    </>
  )
}
