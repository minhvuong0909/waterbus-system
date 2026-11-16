import { useState } from 'react'
import QrCode from '../../features/tickets/components/QrCode'
import { Button, ErrorState, Notice, PageHeader, PageLoader, Pagination, Panel, SelectField, TextField } from '../../components/ui'
import ScannerDeviceRegistration from '../../features/admin/components/ScannerDeviceRegistration'
import {
  cancelScannerInvitation, changeScannerState, listDevicesPage, listScannerAudit, requestScannerInvitation, reviewScannerEvent,
  updateScannerLabel,
} from '../../features/admin/services/scannerAssignmentService'
import type { ScannerDevice, ScannerStatus } from '../../features/admin/services/scannerAssignmentService'
import { getStations } from '../../features/admin/services/stationService'
import type { AdminStation } from '../../features/admin/types'
import { useDocumentTitle, useFetch, useNow, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatDateTime } from '../../utils/time'

const statusLabels: Record<ScannerStatus, string> = {
  Active: 'Paired', PendingActivation: 'Pending activation', Unbound: 'Station required', Suspended: 'Suspended', Revoked: 'Revoked',
}

export default function ScannerManagementPage() {
  useDocumentTitle('Scanner management')
  const [search, setSearch] = useState('')
  const [stationId, setStationId] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [detailId, setDetailId] = useState('')
  const [created, setCreated] = useState(false)
  const stations = useFetch(getStations)
  const devices = useFetch(() => listDevicesPage(page, search, stationId, status), [page, search, stationId, status])
  const activeStations = stations.data?.filter((station) => station.isActive) ?? []

  return (
    <>
      <PageHeader title="Scanner management" description="Register station-bound scanners and manage their pairing." />
      <Panel title="Register scanner" className="space-y-3">
        {stations.error && <ErrorState compact onRetry={stations.retry} />}
        {stations.data && <ScannerDeviceRegistration stations={activeStations} onRegistered={(id) => {
          setCreated(true)
          setSearch('')
          setStationId('')
          setStatus('')
          setPage(1)
          setDetailId(id)
          devices.retry()
        }} />}
        {created && <Notice tone="success">{t('Scanner created. Open its details to generate an activation QR.')}</Notice>}
      </Panel>
      <Panel title="Scanners" className="mt-4 space-y-4">
        <Button variant="neutral" size="sm" disabled={devices.fetching} onClick={devices.retry}>{t('Refresh devices')}</Button>
        <div className="grid gap-3 md:grid-cols-3">
          <TextField label="Search device code or label" value={search} maxLength={50} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
          <SelectField label="Filter by station" value={stationId} onChange={(e) => { setStationId(e.target.value); setPage(1) }}>
            <option value="">{t('All stations')}</option>
            {stations.data?.map((station) => <option key={station.id} value={station.id}>{station.name}</option>)}
          </SelectField>
          <SelectField label="Filter by status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
            <option value="">{t('All statuses')}</option>
            {Object.entries(statusLabels).map(([value, label]) => <option key={value} value={value}>{t(label)}</option>)}
          </SelectField>
        </div>
        {devices.error && <ErrorState compact onRetry={devices.retry} />}
        {devices.fetching && <PageLoader />}
        {devices.data && !devices.fetching && <>
          {devices.data.items.length === 0 && <p>{t('No scanners found.')}</p>}
          <ul className="divide-y divide-outline-variant/30">
            {devices.data.items.map((device) => <li key={device.id} className="py-3">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <p className="font-semibold">{device.deviceCode}{device.label && ` · ${device.label}`}</p>
                  <p className="text-sm text-on-surface-variant">{device.stationName ?? t('Station required')} · {t(statusLabels[device.status])}</p>
                </div>
                <Button variant="neutral" size="sm" aria-expanded={detailId === device.id}
                  onClick={() => setDetailId(detailId === device.id ? '' : device.id)}>{t('Details')}</Button>
              </div>
              {detailId === device.id && <ScannerDetails device={device} stations={activeStations} onChanged={devices.retry} />}
            </li>)}
          </ul>
          <Pagination pageNumber={page} totalPages={devices.data.totalPages} onChange={setPage} />
        </>}
      </Panel>
    </>
  )
}

type LifecycleAction = 'activate' | 'deactivate' | 'revoke' | 'transfer'

const actionLabels: Record<LifecycleAction, string> = {
  activate: 'Prepare for re-pairing', deactivate: 'Suspend scanner', revoke: 'Revoke scanner', transfer: 'Transfer scanner',
}

function ScannerDetails({ device, stations, onChanged }: { device: ScannerDevice; stations: AdminStation[]; onChanged: () => void }) {
  const { run, busy, error } = useSubmit()
  const now = useNow(1000)
  const [qr, setQr] = useState<{ activation: string; expiresAt: string } | null>(null)
  const [inviteUntil, setInviteUntil] = useState(device.invitationExpiresAt)
  const [target, setTarget] = useState('')
  const [action, setAction] = useState<LifecycleAction | null>(null)
  const [showAudit, setShowAudit] = useState(false)
  const [label, setLabel] = useState(device.label ?? '')

  async function generate() {
    setQr(null)
    await run(async () => {
      const invitation = await requestScannerInvitation(device.id)
      setInviteUntil(invitation.expiresAt)
      setQr(invitation)
    })
  }

  async function confirm() {
    if (!action || busy) return
    await run(async () => {
      await changeScannerState(device.id, action, action === 'transfer' ? target : undefined)
      setQr(null)
      setAction(null)
      onChanged()
    })
  }

  return (
    <div className="mt-3 space-y-3 rounded-xl bg-surface-container-low p-4">
      <p className="text-sm">{t('Current or upcoming assignments {assignments} · Check-in events {events}', { assignments: device.activeAssignmentCount, events: device.checkInEventCount })}</p>
      {device.status === 'PendingActivation' && <div className="flex flex-wrap gap-2">
        <Button disabled={busy} onClick={generate}>{t('Generate activation QR')}</Button>
        {inviteUntil && Date.parse(inviteUntil) > now && <Button variant="danger" disabled={busy} onClick={() => run(async () => {
          await cancelScannerInvitation(device.id)
          setQr(null)
          setInviteUntil(null)
        })}>{t('Revoke invitation')}</Button>}
      </div>}
      {qr && Date.parse(qr.expiresAt) > now && <figure>
        <QrCode value={qr.activation} size={260} label={t('One-time scanner activation QR')} />
        <figcaption>{t('Scan from the signed-in Staff Scanner app by {time}.', { time: formatDateTime(qr.expiresAt) })}</figcaption>
      </figure>}
      <div className="flex flex-wrap gap-2">
        {device.status === 'Active' && <Button variant="neutral" disabled={busy} onClick={() => setAction('deactivate')}>{t('Suspend scanner')}</Button>}
        {device.status !== 'Revoked' && device.status !== 'Unbound' && <Button variant="danger" disabled={busy} onClick={() => setAction('revoke')}>{t('Revoke scanner')}</Button>}
        {(device.status === 'Active' || device.status === 'Suspended' || device.status === 'Revoked') &&
          <Button variant="neutral" disabled={busy} onClick={() => setAction('activate')}>{t('Prepare for re-pairing')}</Button>}
        <Button variant="neutral" onClick={() => setShowAudit(!showAudit)}>{showAudit ? t('Hide history') : t('View check-in history')}</Button>
      </div>
      <div className="flex flex-wrap items-end gap-2">
        <TextField label="Device label (optional)" value={label} maxLength={100} onChange={(e) => setLabel(e.target.value)} />
        <Button variant="neutral" disabled={busy || label.trim() === (device.label ?? '')}
          onClick={() => run(async () => { await updateScannerLabel(device.id, label); onChanged() })}>{t('Save label')}</Button>
      </div>
      <div className="flex flex-wrap items-end gap-2">
        <SelectField label={device.stationId ? 'Transfer to station' : 'Bind legacy scanner to station'} value={target}
          onChange={(e) => { setTarget(e.target.value); setAction(null) }}>
          <option value="">{t('Choose a station')}</option>
          {stations.filter((station) => station.id !== device.stationId)
            .map((station) => <option key={station.id} value={station.id}>{station.name}</option>)}
        </SelectField>
        <Button variant="neutral" disabled={!target || busy} onClick={() => setAction('transfer')}>{t('Review transfer')}</Button>
      </div>
      {action && <div className="space-y-2">
        <p>{action === 'transfer'
          ? t('{action} {code} to {station}?', { action: t(actionLabels[action]), code: device.deviceCode, station: stations.find((station) => station.id === target)?.name ?? '' })
          : t('{action} {code}?', { action: t(actionLabels[action]), code: device.deviceCode })}</p>
        <p className="text-sm">{t('This ends the current enrollment, cancels its invitation, and ends its Primary Offline assignments.')}</p>
        <div className="flex gap-2">
          <Button variant="danger" disabled={busy} onClick={confirm}>{t('Confirm')}</Button>
          <Button variant="neutral" disabled={busy} onClick={() => setAction(null)}>{t('Cancel')}</Button>
        </div>
      </div>}
      {error && <Notice tone="error">{error}</Notice>}
      {showAudit && <ScannerAudit id={device.id} />}
    </div>
  )
}

function ScannerAudit({ id }: { id: string }) {
  const [page, setPage] = useState(1)
  const audit = useFetch(() => listScannerAudit(id, page), [id, page])
  const [review, setReview] = useState<{ eventId: string; approve: boolean } | null>(null)
  const [reason, setReason] = useState('')
  const { run, busy, error } = useSubmit()

  async function resolve() {
    if (!review || !reason.trim() || busy) return
    await run(async () => {
      await reviewScannerEvent(id, review.eventId, review.approve, reason.trim())
      setReview(null)
      setReason('')
      audit.retry()
    })
  }
  if (audit.error) return <ErrorState compact onRetry={audit.retry} />
  if (!audit.data || audit.fetching) return <PageLoader />
  return (
    <section aria-label={t('Check-in history')} className="space-y-2">
      {audit.data.items.length === 0 && <p>{t('No check-in events recorded.')}</p>}
      <ul className="divide-y divide-outline-variant/30">
        {audit.data.items.map((item) => <li key={item.id} className="py-2 text-sm">
          <p>{item.outcome} · {formatDateTime(item.occurredAtDevice)}</p>
          <p>{t('Ticket {ticket} · Staff {staff}', { ticket: item.ticketId, staff: item.staffAccountId })}</p>
          {item.reviewNote && <p>{t('Review reason')} · {item.reviewNote}</p>}
          {item.outcome === 'ReviewRequired' && <div className="mt-2 space-y-2">
            <div className="flex gap-2">
              <Button size="sm" variant="neutral" disabled={busy} onClick={() => setReview({ eventId: item.id, approve: true })}>{t('Approve historical check-in')}</Button>
              <Button size="sm" variant="danger" disabled={busy} onClick={() => setReview({ eventId: item.id, approve: false })}>{t('Reject event')}</Button>
            </div>
            {review?.eventId === item.id && <>
              <TextField label="Review reason" value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
              <div className="flex gap-2">
                <Button disabled={!reason.trim() || busy} onClick={resolve}>{review.approve ? t('Confirm approval') : t('Confirm rejection')}</Button>
                <Button variant="neutral" disabled={busy} onClick={() => { setReview(null); setReason('') }}>{t('Cancel')}</Button>
              </div>
              {error && <Notice tone="error">{error}</Notice>}
            </>}
          </div>}
        </li>)}
      </ul>
      <Pagination pageNumber={page} totalPages={audit.data.totalPages} onChange={setPage} />
    </section>
  )
}
