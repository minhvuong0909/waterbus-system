import { useState } from 'react'
import { Button, ErrorState, Icon, Modal, Notice, PageLoader } from '../../components/ui'
import GpsSharePanel from '../../features/captain/components/GpsSharePanel'
import { useCockpitAudio } from '../../features/captain/hooks/useCockpitAudio'
import { useFleetHub } from '../../features/operations/hooks/useFleetHub'
import { getOperationTrips, operateTrip } from '../../features/operations/services/operationsService'
import type { AdminTrip, OperationType } from '../../features/operations/types'
import { isUpcomingOrRunning } from '../../features/operations/utils'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { useDocumentTitle, useFetch, useReload, useSubmit } from '../../hooks'
import { t } from '../../i18n'
import { formatClock, formatDateTime } from '../../utils/time'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

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
      <Icon name={icon} className="text-3xl" />
      {label}
    </button>
  )
}

export default function CaptainPortalPage() {
  useDocumentTitle('Captain Portal')
  const { user } = useAuth()
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [sosOpen, setSosOpen] = useState(false)
  const [note, setNote] = useState('')
  const [sosSent, setSosSent] = useState(false)
  // `null` = follow the trip type: sightseeing trips narrate by default, regular trips do not.
  const [audioOverride, setAudioOverride] = useState<boolean | null>(null)
  const action = useSubmit()

  const open = (trips.data ?? []).filter((x) => isUpcomingOrRunning(x.status))
  // Default to the trip on the river, otherwise the next one.
  const trip: AdminTrip | undefined =
    open.find((x) => x.id === selectedId) ?? open.find((x) => x.status === 'EnRoute') ?? open[0]

  const narrating = audioOverride ?? trip?.tripType === 'Sightseeing'

  // Quản lý phát âm thanh buồng lái tự động theo hành trình tàu (gọi trên đầu theo chuẩn Rules of Hooks)
  const { isPlaying, nowPlaying, handleStopAnnouncement } = useCockpitAudio({ trip, narrating })

  // The captain follows their own boat: a status change by the admin shows up at once.
  const boatIds = [...new Set((trips.data ?? []).map((x) => x.boatId).filter(Boolean))]
  useFleetHub(
    {
      onTripStatus: reload,
      onReconnected: reload,
      onStopAnnouncement: handleStopAnnouncement,
    },
    boatIds.length > 0,
    boatIds,
  )

  if (trips.error) return <ErrorState onRetry={trips.retry} />
  if (!trips.data) return <PageLoader />

  if (!trip) {
    return (
      <p className="rounded-2xl bg-white p-8 text-center text-on-surface-variant">
        {t('You have no trips to run right now.')}
      </p>
    )
  }

  const can = (op: OperationType) => trip.allowedOperations.includes(op)
  const perform = async (type: OperationType, reason: string) => {
    const done = await action.run(() => operateTrip(trip.id, type, reason))
    if (done) reload()
    return Boolean(done)
  }

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
              {formatDateTime(x.departureTime)} • {x.routeName}
            </option>
          ))}
        </select>
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="font-display text-3xl font-bold text-deep-river">
              {formatClock(new Date(trip.departureTime))} → {formatClock(new Date(trip.arrivalTime))}
            </div>
            <div className="text-sm text-on-surface-variant">
              {formatDateTime(trip.departureTime)} • {trip.routeName} • {trip.boatCode ?? '—'} • {t(trip.tripType === 'Commuter' ? 'Regular' : 'Sightseeing')}
            </div>
          </div>
          <TripStatusBadge status={trip.status} />
        </div>

        {/* Thanh trạng thái nhỏ gọn khi đang phát âm thanh thông báo */}
        {isPlaying && nowPlaying && (
          <div className="mt-4 flex items-center gap-2 rounded-xl bg-teal-flow/10 px-3.5 py-2 text-xs font-medium text-teal-900 border border-teal-200">
            <span className="relative flex h-2 w-2">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-teal-500 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-teal-600"></span>
            </span>
            <span className="truncate">🔊 <strong>{nowPlaying.title}:</strong> &ldquo;{nowPlaying.text}&rdquo;</span>
          </div>
        )}
      </div>

      {user && <GpsSharePanel userId={user.id} />}

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
        <Notice tone="error">{t('This trip is on hold. Wait for the admin to resume it.')}</Notice>
      )}
      {action.error && <Notice tone="error">{action.error}</Notice>}
      {sosSent && <Notice tone="success">{t('SOS sent. The admin has been alerted.')}</Notice>}

      {sosOpen && (
        <Modal title="Send an SOS" onClose={() => setSosOpen(false)}>
          <p className="text-sm text-on-surface-variant">
            {t('This alerts the admin right away. Only use it in an emergency.')}
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
            <Button size="sm" variant="neutral" onClick={() => setSosOpen(false)}>
              {t('Cancel')}
            </Button>
            <Button size="sm"
              variant="danger"
              disabled={action.busy}
              onClick={async () => {
                if (await perform('SOS', note.trim() || t('Emergency reported by the captain.'))) {
                  setSosOpen(false)
                  setSosSent(true)
                }
              }}
            >
              {t('Send SOS')}
            </Button>
          </div>
        </Modal>
      )}
    </div>
  )
}
