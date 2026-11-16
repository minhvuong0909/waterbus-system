import { useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub, hubErrorMessage } from '../../../services/realtime'
import type { RealtimeStatus } from '../../../services/realtime'
import type { BoatFix } from '../../tracking/types'
import type { BoatPositionEvent } from '../../operations/services/operationsService'

/** Theo dõi đúng tàu được phân công; giữ vị trí cuối khi mất GPS/kết nối. */
export function useCaptainGps(boatId: string, onRefresh: () => void) {
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const [online, setOnline] = useState(navigator.onLine)
  const [fix, setFix] = useState<(BoatFix & { boatId: string }) | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [now, setNow] = useState(() => Date.now())
  const connection = useRef<HubConnection | null>(null)
  const refresh = useRef(onRefresh)
  useEffect(() => {
    refresh.current = onRefresh
  })

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 5000)
    const updateOnline = () => {
      setOnline(navigator.onLine)
      if (navigator.onLine) refresh.current()
    }
    window.addEventListener('online', updateOnline)
    window.addEventListener('offline', updateOnline)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('online', updateOnline)
      window.removeEventListener('offline', updateOnline)
    }
  }, [])

  useEffect(() => {
    if (!boatId) return
    let active = true
    const apply = (event: BoatPositionEvent) => {
      if (!active || event.boatId !== boatId) return
      setFix((previous) =>
        previous?.boatId === boatId &&
        Date.parse(previous.observedAt) >= Date.parse(event.observedAt)
          ? previous
          : {
              boatId,
              latitude: event.lat,
              longitude: event.lng,
              speedKnots: event.speedKnots,
              observedAt: event.observedAt,
            },
      )
      setError(null)
    }
    const stop = connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.boatPosition]: apply,
        [HUB_EVENTS.tripStatusChanged]: () => refresh.current(),
      },
      onConnected: async (hub) => {
        await hub.invoke('JoinBoat', boatId)
        if (active) connection.current = hub
      },
      onReconnected: () => refresh.current(),
      onStatus: (next) => {
        if (!active) return
        setStatus(next)
        if (next !== 'live') connection.current = null
      },
    })
    return () => {
      active = false
      connection.current = null
      stop()
    }
  }, [boatId])

  useEffect(() => {
    // Luôn chia sẻ GPS của thiết bị trên tàu khi có chuyến; không có nút tắt/bật.
    if (!boatId || !navigator.geolocation) return
    let active = true
    let lastSent = 0
    let sending = false
    const watch = navigator.geolocation.watchPosition(
      async ({ coords, timestamp }) => {
        const hub = connection.current
        if (!active || !hub || sending || timestamp - lastSent < 3000) return
        sending = true
        lastSent = timestamp
        const observedAt = new Date(timestamp).toISOString()
        const speedKnots =
          coords.speed == null ? null : Math.round(coords.speed * 1.943844 * 100) / 100
        try {
          const accepted = await hub.invoke<boolean>(
            'SendPosition',
            boatId,
            coords.latitude,
            coords.longitude,
            speedKnots,
            observedAt,
          )
          if (!active) return
          if (accepted) {
            setFix((previous) =>
              previous?.boatId === boatId && Date.parse(previous.observedAt) >= timestamp
                ? previous
                : {
                    boatId,
                    latitude: coords.latitude,
                    longitude: coords.longitude,
                    speedKnots,
                    observedAt,
                  },
            )
            setError(null)
          }
        } catch (e) {
          if (active) setError(hubErrorMessage(e))
        } finally {
          sending = false
        }
      },
      () => {
        // GPS luôn được thử chia sẻ; thiết bị không cấp quyền/không có tín hiệu thì im lặng (vị trí vẫn đến từ máy chủ,
        // trạng thái hiện ở nhãn "GPS chưa cập nhật" trên thanh đầu trang).
      },
      { enableHighAccuracy: true, maximumAge: 0, timeout: 20000 },
    )
    return () => {
      active = false
      navigator.geolocation.clearWatch(watch)
    }
  }, [boatId])

  const position = fix?.boatId === boatId ? fix : null
  const connected = online && status === 'live'
  const fresh = Boolean(
    position && now - Date.parse(position.observedAt) < 60000 && connected && !error,
  )
  return { connected, status, position, fresh, error }
}
