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
  /** Trạng thái một chuyến vừa đổi (Admin hoặc thuyền trưởng vừa thao tác). */
  onTripStatus?: (event: TripStatusEvent) => void
  /** Thuyền trưởng báo khẩn cấp. */
  onIncident?: (event: IncidentRaisedEvent) => void
  /** Sau khi kết nối lại: tải lại những gì có thể đã lỡ trong lúc mất kết nối. */
  onReconnected?: () => void
}

/**
 * Kết nối hub theo dõi tàu: vị trí tàu, cảnh báo thời tiết, báo khẩn cấp và đổi trạng thái chuyến ngay khi xảy ra.
 * Admin theo dõi cả đội tàu (group `fleet`); truyền `boatIds` cho thuyền trưởng, người chỉ theo dõi được tàu của
 * mình (group `boat:{id}`). Handler đổi giữa các lần render mà không phải kết nối lại.
 */
export function useFleetHub(handlers: FleetHubHandlers, enabled = true, boatIds?: string[]): RealtimeStatus {
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const ref = useRef(handlers)
  useEffect(() => {
    ref.current = handlers
  })
  // Khoá ổn định: effect không được kết nối lại chỉ vì mảng được tạo mới.
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
 * Vị trí mới nhất của mỗi tàu: ảnh chụp từ REST trước, rồi từng sự kiện `boatPosition`. Điểm cũ hơn điểm đang hiện
 * bị bỏ qua, để sự kiện đến muộn không kéo tàu lùi lại.
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
