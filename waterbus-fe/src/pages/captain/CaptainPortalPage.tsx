import { useState } from 'react'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { ActionButton, Modal, Notice, StatusBadge } from '../../features/admin/components/AdminUi'
import GpsSharePanel from '../../features/admin/components/GpsSharePanel'
import { USE_MOCK } from '../../constant/env'
import { useReload, useSubmit } from '../../features/admin/hooks/useAdmin'
import { useFleetHub } from '../../features/admin/hooks/useFleetHub'
import { getBoats, getOperationTrips, getRoutes, operateTrip } from '../../features/admin/services/adminService'
import type { AdminTrip, OperationType } from '../../features/admin/types'
import {
  allowedOperationsOf,
  formatClockTime,
  isUpcomingOrRunning,
  tripRouteName,
} from '../../features/admin/utils'
import { useAuth } from '../../features/authentication/hook/authenticationHook'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'

/** Large touch target for use on board. */
function BigButton({
  icon,
  label,
  tone,
  disabled,
  onClick,
}: {
  icon: string
  label: string
  tone: 'teal' | 'dark' | 'red'
  disabled?: boolean
  onClick: () => void
}) {
  const tones = {
    teal: 'bg-teal-flow text-on-primary hover:bg-secondary',
    dark: 'bg-deep-river text-on-primary hover:bg-primary-container',
    red: 'bg-coral-glow text-white hover:opacity-90',
  }
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`flex min-h-[5.5rem] flex-1 flex-col items-center justify-center gap-1 rounded-2xl px-4 py-3 text-lg font-bold shadow-sm transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${tones[tone]}`}
    >
      <Icon name={icon} className="text-[32px]" />
      {label}
    </button>
  )
}

export default function CaptainPortalPage() {
  useDocumentTitle('Captain Portal')
  const { user } = useAuth()
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [sosOpen, setSosOpen] = useState(false)
  const [note, setNote] = useState('')
  const [sosSent, setSosSent] = useState(false)
  // `null` = follow the trip type: sightseeing trips narrate by default, regular trips do not.
  const [audioOverride, setAudioOverride] = useState<boolean | null>(null)
  const action = useSubmit()
  // The captain follows their own boat: a status change by the dispatcher shows up at once.
  const boatIds = [...new Set((trips.data ?? []).map((x) => x.boatId).filter(Boolean))]
  useFleetHub({ onTripStatus: reload, onReconnected: reload }, !USE_MOCK && boatIds.length > 0, boatIds)
  const state = combineFetches(trips, routes, boats)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !routes.data || !boats.data) return <PageLoader />

  const open = trips.data.filter((x) => isUpcomingOrRunning(x.status))
  // Default to the trip on the river, otherwise the next one.
  const trip: AdminTrip | undefined =
    open.find((x) => x.id === selectedId) ?? open.find((x) => x.status === 'EnRoute') ?? open[0]

  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'
  const boatName = (id: string) => boats.data?.find((b) => b.id === id)?.name ?? '—'
  const by = user?.fullName ?? ''

  if (!trip) {
    return (
      <p className="rounded-2xl bg-white p-8 text-center text-on-surface-variant">
        {t('You have no trips to run right now.')}
      </p>
    )
  }

  const can = (op: OperationType) => allowedOperationsOf(trip).includes(op)
  const perform = async (type: OperationType, reason: string) => {
    const done = await action.run(() => operateTrip(trip.id, type, reason, by))
    if (done) reload()
    return Boolean(done)
  }
  const narrating = audioOverride ?? trip.tripType === 'Sightseeing'

  return (
    <div className="space-y-4">
      <div className="rounded-2xl bg-white p-5 shadow-xs">
        <label htmlFor="captain-trip" className="mb-1 block text-xs font-semibold uppercase tracking-wide text-on-surface-variant">
          {t('Trip')}
        </label>
        <select
          id="captain-trip"
          value={trip.id}
          onChange={(e) => {
            setSelectedId(e.target.value)
            setAudioOverride(null)
            setSosSent(false)
          }}
          className="w-full rounded-lg border border-outline-variant px-3 py-2.5 text-base text-deep-river"
        >
          {open.map((x) => (
            <option key={x.id} value={x.id}>
              {formatClockTime(x.departureTime)} • {tripRouteName(x, routeName)}
            </option>
          ))}
        </select>
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="font-headline-md text-3xl font-bold text-deep-river">
              {formatClockTime(trip.departureTime)} → {formatClockTime(trip.arrivalTime)}
            </div>
            <div className="text-sm text-on-surface-variant">
              {tripRouteName(trip, routeName)} • {trip.boatCode ?? boatName(trip.boatId)} • {t(trip.tripType === 'Commuter' ? 'Regular' : 'Sightseeing')}
            </div>
          </div>
          <StatusBadge status={trip.status} />
        </div>
      </div>

      {!USE_MOCK && user && <GpsSharePanel userId={user.id} />}

      <div className="flex gap-3">
        <BigButton icon="play_arrow" label={t('Start trip')} tone="teal" disabled={!can('Start') || action.busy} onClick={() => perform('Start', t('Started by the captain.'))} />
        <BigButton icon="anchor" label={t('Arrived')} tone="dark" disabled={!can('Arrive') || action.busy} onClick={() => perform('Arrive', t('Arrived at the pier.'))} />
      </div>

      <div className="flex gap-3">
        <BigButton
          icon={narrating ? 'volume_up' : 'volume_off'}
          label={narrating ? t('Narration on') : t('Narration off')}
          tone="dark"
          onClick={() => setAudioOverride(!narrating)}
        />
        <BigButton icon="sos" label="SOS" tone="red" onClick={() => { setSosSent(false); setSosOpen(true) }} />
      </div>
      {trip.tripType !== 'Sightseeing' && (
        <p className="text-center text-xs text-on-surface-variant">
          {t('Narration is meant for sightseeing trips. Regular trips start with it off.')}
        </p>
      )}

      {trip.status === 'Suspended' && (
        <Notice tone="error">{t('This trip is on hold. Wait for the dispatcher to resume it.')}</Notice>
      )}
      {action.error && <Notice tone="error">{action.error}</Notice>}
      {sosSent && <Notice tone="success">{t('SOS sent. The dispatcher has been alerted.')}</Notice>}

      {sosOpen && (
        <Modal title="Send an SOS" onClose={() => setSosOpen(false)}>
          <p className="text-sm text-on-surface-variant">
            {t('This alerts the dispatcher right away. Only use it in an emergency.')}
          </p>
          <label htmlFor="sos-note" className="mb-1 mt-3 block text-xs font-semibold text-on-surface-variant">
            {t('What is happening? (optional)')}
          </label>
          <textarea
            id="sos-note"
            rows={3}
            value={note}
            onChange={(e) => setNote(e.target.value)}
            className="w-full rounded-lg border border-outline-variant px-3 py-2 text-sm text-deep-river"
          />
          <div className="mt-4 flex justify-end gap-2">
            <ActionButton tone="secondary" onClick={() => setSosOpen(false)}>
              {t('Cancel')}
            </ActionButton>
            <ActionButton
              tone="danger"
              disabled={action.busy}
              onClick={async () => {
                if (await perform('SOS', note.trim() || t('Emergency reported by the captain.'))) {
                  setSosOpen(false)
                  setNote('')
                  setSosSent(true)
                }
              }}
            >
              {t('Send SOS')}
            </ActionButton>
          </div>
        </Modal>
      )}
    </div>
  )
}
