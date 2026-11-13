import { useCallback, useEffect, useRef, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { RealtimeStatus } from '../../../services/realtime'
import {
  fromPositionEvent,
  getLatestBoatPositions,
} from '../services/operationsService'
import type {
  BoatPositionEvent,
  IncidentRaisedEvent,
  TripStatusEvent,
  WeatherAlertEvent,
} from '../services/operationsService'
import type { LiveBoatPosition } from '../types'

interface FleetHubHandlers {
  onPosition?: (position: LiveBoatPosition) => void
  onAlert?: (alert: WeatherAlertEvent) => void
  /** A trip's status changed (an admin or the captain did something). */
  onTripStatus?: (event: TripStatusEvent) => void
  /** A captain reported an emergency. */
  onIncident?: (event: IncidentRaisedEvent) => void
  /** After a reconnect: reload what may have been missed while offline. */
  onReconnected?: () => void
}

/**
 * Connection to the boat-tracking hub: boat positions, weather alerts, emergencies and trip status changes
 * as they happen. An admin follows the whole fleet (group `fleet`); pass `boatIds` for a captain, who can
 * only follow their own boat (group `boat:{id}`). Handlers can change between renders without reconnecting.
 */
export function useFleetHub(handlers: FleetHubHandlers, enabled = true, boatIds?: string[]): RealtimeStatus {
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const ref = useRef(handlers)
  useEffect(() => {
    ref.current = handlers
  })
  // A stable key: the effect must not reconnect just because the array was re-created.
  const boatKey = boatIds?.join(',')

  useEffect(() => {
    if (!enabled) return
    const boats = boatKey ? boatKey.split(',') : undefined
    return connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.boatPosition]: (event: BoatPositionEvent) => ref.current.onPosition?.(fromPositionEvent(event)),
        [HUB_EVENTS.weatherAlert]: (event: WeatherAlertEvent) => ref.current.onAlert?.(event),
        [HUB_EVENTS.tripStatusChanged]: (event: TripStatusEvent) => ref.current.onTripStatus?.(event),
        [HUB_EVENTS.incidentRaised]: (event: IncidentRaisedEvent) => ref.current.onIncident?.(event),
      },
      onConnected: async (connection) => {
        if (boats) {
          for (const id of boats) await connection.invoke('JoinBoat', id)
        } else {
          await connection.invoke('JoinFleet')
        }
      },
      onReconnected: () => ref.current.onReconnected?.(),
      onStatus: setStatus,
    })
  }, [enabled, boatKey])

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
