import { useCallback, useEffect, useRef, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { RealtimeStatus } from '../../../services/realtime'
import {
  fromPositionEvent,
  getLatestBoatPositions,
} from '../services/operationsService'
import type { BoatPositionEvent, WeatherAlertEvent } from '../services/operationsService'
import type { LiveBoatPosition } from '../types'

interface FleetHubHandlers {
  onPosition?: (position: LiveBoatPosition) => void
  onAlert?: (alert: WeatherAlertEvent) => void
  /** After a reconnect: reload what may have been missed while offline. */
  onReconnected?: () => void
}

/**
 * Admin connection to the boat-tracking hub (group `fleet`): boat positions and weather alerts as
 * they happen. Handlers can change between renders without reconnecting.
 */
export function useFleetHub(handlers: FleetHubHandlers, enabled = true): RealtimeStatus {
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const ref = useRef(handlers)
  useEffect(() => {
    ref.current = handlers
  })

  useEffect(() => {
    if (!enabled) return
    return connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.boatPosition]: (event: BoatPositionEvent) => ref.current.onPosition?.(fromPositionEvent(event)),
        [HUB_EVENTS.weatherAlert]: (event: WeatherAlertEvent) => ref.current.onAlert?.(event),
      },
      onConnected: (connection) => connection.invoke('JoinFleet'),
      onReconnected: () => ref.current.onReconnected?.(),
      onStatus: setStatus,
    })
  }, [enabled])

  return status
}

/**
 * Latest position of every boat: a REST snapshot first, then each `boatPosition` event. Points older
 * than what is shown are ignored, so a late event cannot move a boat backwards.
 */
export function useLiveFleet(onAlert?: (alert: WeatherAlertEvent) => void) {
  const [positions, setPositions] = useState<Record<string, LiveBoatPosition>>({})
  const [error, setError] = useState<Error>()
  const [loaded, setLoaded] = useState(false)

  const apply = useCallback((incoming: LiveBoatPosition[]) => {
    setPositions((current) => {
      const next = { ...current }
      incoming.forEach((p) => {
        const shown = next[p.boatId]
        if (!shown || Date.parse(p.observedAt) > Date.parse(shown.observedAt)) next[p.boatId] = p
      })
      return next
    })
  }, [])

  const loadSnapshot = useCallback(() => {
    getLatestBoatPositions()
      .then((snapshot) => {
        apply(snapshot)
        setError(undefined)
      })
      .catch((e: Error) => setError(e))
      .finally(() => setLoaded(true))
  }, [apply])

  useEffect(loadSnapshot, [loadSnapshot])

  const status = useFleetHub({
    onPosition: (p) => apply([p]),
    onAlert,
    onReconnected: loadSnapshot,
  })

  return { positions: Object.values(positions), status, error, loaded, retry: loadSnapshot }
}
