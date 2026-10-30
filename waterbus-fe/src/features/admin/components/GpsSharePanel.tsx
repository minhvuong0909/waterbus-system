import { useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { Icon } from '../../../components/ui'
import { HUBS } from '../../../constant/apiEndpoints'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { connectHub, hubErrorMessage } from '../../../services/realtime'
import type { RealtimeStatus } from '../../../services/realtime'
import { formatClockWithSeconds } from '../../../utils/time'
import { getBoats } from '../services/boatService'
import { Notice, Panel } from './AdminUi'

/** Send at most one position every few seconds, whatever rate the device reports at. */
const MIN_SEND_INTERVAL_MS = 3000
const MS_TO_KNOTS = 1.943844

/**
 * Lets the captain share the boat's GPS from this device (BR-TRK-01: position of the Boat).
 * Positions go to the boat-tracking hub (`SendPosition`); the backend only accepts them from the
 * captain assigned to the boat. Browsers allow geolocation only on HTTPS or localhost.
 */
export default function GpsSharePanel({ userId }: { userId: string }) {
  const boats = useFetch(getBoats)
  const boat = boats.data?.find((b) => b.captainUserId?.toLowerCase() === userId.toLowerCase())
  const [sharing, setSharing] = useState(false)
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const [lastSent, setLastSent] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  // The backend ignores a point older than the boat's latest one (e.g. while the simulator also reports).
  const [ignored, setIgnored] = useState(false)
  const connection = useRef<HubConnection | null>(null)

  useEffect(() => {
    if (!sharing || !boat) return

    let lastSentAt = 0
    const stopHub = connectHub(HUBS.boatTracking, {
      handlers: {},
      onConnected: async (c) => {
        connection.current = c
      },
      onStatus: setStatus,
    })

    const watchId = navigator.geolocation.watchPosition(
      async (position) => {
        const hub = connection.current
        if (!hub || position.timestamp - lastSentAt < MIN_SEND_INTERVAL_MS) return
        lastSentAt = position.timestamp
        const { latitude, longitude, speed } = position.coords
        const observedAt = new Date(position.timestamp).toISOString()
        try {
          const accepted = await hub.invoke<boolean>(
            'SendPosition',
            boat.id,
            latitude,
            longitude,
            speed != null ? Math.round(speed * MS_TO_KNOTS * 100) / 100 : null,
            observedAt,
          )
          setLastSent(observedAt)
          setIgnored(!accepted)
          setError(null)
        } catch (e) {
          setError(hubErrorMessage(e))
        }
      },
      (geoError) => setError(geoError.message || t('Could not read the location of this device.')),
      { enableHighAccuracy: true, maximumAge: 0, timeout: 20_000 },
    )

    return () => {
      navigator.geolocation.clearWatch(watchId)
      connection.current = null
      stopHub()
    }
  }, [sharing, boat])

  if (boats.loading) return null

  return (
    <Panel title="Boat GPS">
      {!boat ? (
        <p className="text-sm text-on-surface-variant">
          {t('No boat is assigned to you. An admin needs to set you as the captain of a boat first.')}
        </p>
      ) : (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="text-sm text-deep-river">
              <span className="font-mono font-bold">{boat.code}</span> • {boat.name}
            </div>
            <button
              type="button"
              onClick={() => {
                if (!sharing && !('geolocation' in navigator)) {
                  setError(t('This device cannot share its location.'))
                  return
                }
                setError(null)
                setSharing((s) => !s)
              }}
              className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-sm font-bold text-on-primary ${
                sharing ? 'bg-coral-glow' : 'bg-teal-flow'
              }`}
            >
              <Icon name={sharing ? 'location_off' : 'my_location'} className="text-[20px]" />
              {sharing ? t('Stop sharing GPS') : t('Share GPS')}
            </button>
          </div>
          {sharing && (
            <p className="text-xs text-on-surface-variant">
              {status === 'live'
                ? lastSent
                  ? ignored
                    ? t('Position at {time} was ignored: the boat already has a newer one.', {
                        time: formatClockWithSeconds(lastSent),
                      })
                    : t('Position sent at {time}.', { time: formatClockWithSeconds(lastSent) })
                  : t('Waiting for the first GPS fix…')
                : t('Connecting…')}
            </p>
          )}
          {error && <Notice tone="error">{error}</Notice>}
        </div>
      )}
    </Panel>
  )
}
