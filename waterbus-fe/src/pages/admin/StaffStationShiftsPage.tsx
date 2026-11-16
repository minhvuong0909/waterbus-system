import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ErrorState, Notice, PageHeader, PageLoader, Pagination, Panel, SelectField, TextField } from '../../components/ui'
import { createShift, listScannerStaff, listShifts, revokeShift } from '../../features/admin/services/scannerAssignmentService'
import type { StaffShift } from '../../features/admin/services/scannerAssignmentService'
import { getStations } from '../../features/admin/services/stationService'
import { useDocumentTitle, useFetch, useNow, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatDateTime, fromVietnamDateTimeInput } from '../../utils/time'

export default function StaffStationShiftsPage() {
  useDocumentTitle('Staff station shifts')
  const staff = useFetch(listScannerStaff)
  const stations = useFetch(getStations)
  const [staffAccountId, setStaff] = useState('')
  const [stationId, setStation] = useState('')
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [page, setPage] = useState(1)
  const shifts = useFetch(() => listShifts(page), [page])
  const [revoking, setRevoking] = useState<StaffShift | null>(null)
  const [notice, setNotice] = useState('')
  const [invalid, setInvalid] = useState(false)
  const { run, busy, error } = useSubmit()
  const now = useNow(30_000)

  function submit(event: FormEvent) {
    event.preventDefault()
    if (!staffAccountId || !stationId || busy) return
    const from = Date.parse(start + ':00+07:00')
    const until = Date.parse(end + ':00+07:00')
    const valid = Number.isFinite(from) && Number.isFinite(until) && until > Math.max(from, now)
    setInvalid(!valid)
    if (!valid) return
    const activeFrom = fromVietnamDateTimeInput(start)
    const activeUntil = fromVietnamDateTimeInput(end)
    void run(async () => {
      await createShift({ staffAccountId, stationId, activeFrom, activeUntil })
      setNotice(t('Staff shift created.'))
      if (page === 1) shifts.retry()
      else setPage(1)
    })
  }

  return (
    <>
      <PageHeader title="Staff station shifts" description="Schedule staff at stations and review previous shifts." />
      <Panel title="Create shift" className="space-y-3">
        {staff.error && <ErrorState compact onRetry={staff.retry} />}
        {stations.error && <ErrorState compact onRetry={stations.retry} />}
        <form className="space-y-3" onSubmit={submit}>
          <fieldset disabled={busy} className="grid gap-3 sm:grid-cols-2">
            <SelectField label="Staff account" value={staffAccountId} onChange={(e) => setStaff(e.target.value)} required>
              <option value="">{t('Choose Staff')}</option>
              {staff.data?.map((item) => <option key={item.id} value={item.id}>{item.fullName} · {item.staffCode ?? item.email}</option>)}
            </SelectField>
            <SelectField label="Station" value={stationId} onChange={(e) => setStation(e.target.value)} required>
              <option value="">{t('Choose a station')}</option>
              {stations.data?.filter((item) => item.isActive).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </SelectField>
            <TextField label="Start" type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} required />
            <TextField label="End" type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} hint="Vietnam time (UTC+07:00)" required />
          </fieldset>
          <p className="text-xs text-on-surface-variant">
            {t('A Staff member is responsible for a stop only if the shift lasts until that stop’s check-in window closes.')}
          </p>
          <Button type="submit" disabled={busy || !staffAccountId || !stationId}>{t('Create shift')}</Button>
        </form>
        {invalid && <Notice tone="error">{t('Enter an end time after the shift start and the current time.')}</Notice>}
        {error && <Notice tone="error">{error}</Notice>}
        {notice && <Notice tone="success">{notice}</Notice>}
      </Panel>
      <Panel title="Shift history" className="mt-4 space-y-3">
        {shifts.error && <ErrorState compact onRetry={shifts.retry} />}
        {shifts.fetching && <PageLoader />}
        {revoking && <div className="space-y-2">
          <p>{t('Revoke {staff}’s shift at {station}?', { staff: revoking.staffName, station: revoking.stationName })}</p>
          <div className="flex gap-2">
            <Button variant="danger" disabled={busy} onClick={() => run(async () => {
              await revokeShift(revoking.id)
              setRevoking(null)
              setNotice(t('Staff shift revoked.'))
              shifts.retry()
            })}>{t('Confirm revoke')}</Button>
            <Button variant="neutral" disabled={busy} onClick={() => setRevoking(null)}>{t('Cancel')}</Button>
          </div>
        </div>}
        {shifts.data && <>
          {shifts.data.items.length === 0 && <p>{t('No shifts scheduled.')}</p>}
          <ul className="divide-y divide-outline-variant/30">
            {shifts.data.items.map((shift) => {
              const status = shift.revokedAt ? 'Revoked' : Date.parse(shift.activeUntil) <= now ? 'Ended'
                : Date.parse(shift.activeFrom) > now ? 'Upcoming' : 'Active'
              return <li key={shift.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                <div>
                  <p className="font-semibold">{shift.staffName} · {shift.stationName}</p>
                  <p className="text-sm">{formatDateTime(shift.activeFrom)} – {formatDateTime(shift.activeUntil)} · {t(status)}</p>
                </div>
                {(status === 'Active' || status === 'Upcoming') && <Button variant="danger" size="sm" disabled={busy}
                  onClick={() => setRevoking(shift)}>{t('Revoke shift')}</Button>}
              </li>
            })}
          </ul>
          <Pagination pageNumber={page} totalPages={shifts.data.totalPages} onChange={setPage} />
        </>}
      </Panel>
    </>
  )
}
