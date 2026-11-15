import { useCallback, useEffect, useRef, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { HubConnection } from '@microsoft/signalr'
import { apiRequest } from '../../../services/apiClient'
import {
  playAzureAnnouncement,
  stopCurrentAudio,
} from '../../admin/services/simulatorService'
import type { StopAnnouncementEvent } from '../../operations/hooks/useFleetHub'
import type { AdminTrip } from '../../operations/types'
import { getAnnouncementSentence } from '../utils/announcement'
import { formatClock } from '../../../utils/time'

export interface AudioTriggerMessage {
  requestId: string
  tripId: string
  boatId: string
  routePoiId: string
  poiId: string
  poiName: string
  audioGuideId: string
  playbackPath: string
  contentType: string
  durationSeconds?: number
  distanceMeters?: number
  triggeredAt: string
}

export interface AudioControlMessage {
  action: 'pause' | 'recheck'
  tripId?: string
  reason: string
  at: string
}

export interface CockpitNowPlaying {
  type: 'station' | 'poi'
  title: string
  text: string
  time: string
}

interface UseCockpitAudioOptions {
  trip?: AdminTrip
  narrating: boolean
}

/**
 * Hook quản lý loa phát thanh buồng lái tự động theo hành trình tàu.
 * Chạy ngầm trong trang Captain:
 * - Khi Captain ấn [Bắt đầu chuyến] (trip chuyển sang EnRoute), hệ thống tự động:
 *   + Phát âm thanh tiếng Việt thông báo trạm bến (rời bến, sắp đến bến, cập bến).
 *   + Tự động phát thuyết minh du lịch tiếng Việt khi qua các điểm danh lam thắng cảnh (nếu bật thuyết minh).
 * - Khi chuyến kết thúc hoặc tạm dừng, tự động tắt loa.
 */
export function useCockpitAudio({ trip, narrating }: UseCockpitAudioOptions) {
  const [isPlaying, setIsPlaying] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<CockpitNowPlaying | null>(null)

  const currentAudioRef = useRef<HTMLAudioElement | null>(null)
  const onboardHubConnectionRef = useRef<HubConnection | null>(null)
  const boatTrackingConnectionRef = useRef<HubConnection | null>(null)

  const tripRef = useRef(trip)
  useEffect(() => {
    tripRef.current = trip
    // Nếu chuyến không ở trạng thái EnRoute, dừng toàn bộ âm thanh
    if (trip?.status !== 'EnRoute') {
      stopCurrentAudio()
      if (currentAudioRef.current) {
        currentAudioRef.current.pause()
        currentAudioRef.current.currentTime = 0
        currentAudioRef.current = null
      }
      setIsPlaying(false)
      setNowPlaying(null)
    }
  }, [trip])

  const narratingRef = useRef(narrating)
  useEffect(() => {
    narratingRef.current = narrating
  }, [narrating])

  const stopAll = useCallback(() => {
    stopCurrentAudio()
    if (currentAudioRef.current) {
      currentAudioRef.current.pause()
      currentAudioRef.current.currentTime = 0
      currentAudioRef.current = null
    }
    setIsPlaying(false)
    setNowPlaying(null)
  }, [])

  // Phát audio file MP3 (thuyết minh du lịch POI)
  const playPoiAudio = useCallback(
    async (message: AudioTriggerMessage) => {
      stopAll()
      const item: CockpitNowPlaying = {
        type: 'poi',
        title: `Thuyết minh: ${message.poiName}`,
        text: `Đang phát thuyết minh danh lam thắng cảnh ${message.poiName}`,
        time: formatClock(new Date()),
      }

      setNowPlaying(item)
      setIsPlaying(true)

      try {
        if (onboardHubConnectionRef.current) {
          try {
            await onboardHubConnectionRef.current.invoke('AcceptPlayback', message.requestId)
          } catch {}
        }

        const blob = await apiRequest<Blob>(message.playbackPath, { responseType: 'blob' })
        const blobUrl = URL.createObjectURL(blob)

        if (onboardHubConnectionRef.current) {
          try {
            await onboardHubConnectionRef.current.invoke(
              'ReportPlaybackState',
              message.requestId,
              'Started',
              null,
            )
          } catch {}
        }

        const audio = new Audio(blobUrl)
        audio.volume = 0.95
        currentAudioRef.current = audio

        audio.onended = () => {
          URL.revokeObjectURL(blobUrl)
          currentAudioRef.current = null
          setIsPlaying(false)
          setNowPlaying(null)
          if (onboardHubConnectionRef.current) {
            void onboardHubConnectionRef.current.invoke(
              'ReportPlaybackState',
              message.requestId,
              'Completed',
              null,
            )
          }
        }

        audio.onerror = () => {
          URL.revokeObjectURL(blobUrl)
          currentAudioRef.current = null
          setIsPlaying(false)
          setNowPlaying(null)
          if (onboardHubConnectionRef.current) {
            void onboardHubConnectionRef.current.invoke(
              'ReportPlaybackState',
              message.requestId,
              'Failed',
              'Lỗi phát file âm thanh',
            )
          }
        }

        await audio.play()
      } catch (err) {
        console.error('[Onboard Audio] Lỗi tải/phát MP3:', err)
        setIsPlaying(false)
        setNowPlaying(null)
      }
    },
    [stopAll],
  )

  // Phát thông báo trạm bến bằng giọng đọc Azure TTS tiếng Việt
  const handleStopAnnouncement = useCallback(
    (event: StopAnnouncementEvent) => {
      const currentTrip = tripRef.current
      // Chỉ phát khi chuyến đang trong hành trình (EnRoute)
      if (!currentTrip || currentTrip.status !== 'EnRoute') return
      // Lọc đúng chuyến tàu đang chọn
      if (event.tripId && event.tripId !== currentTrip.id) return
      if (event.boatId && currentTrip.boatId && event.boatId !== currentTrip.boatId) return

      const sentence = getAnnouncementSentence(event)
      const label =
        event.type === 'departing'
          ? 'Rời bến'
          : event.type === 'near'
            ? 'Sắp đến bến'
            : 'Cập bến'

      console.log(`[Cockpit Audio] Nhận sự kiện (${label}): ${event.stationName} -> Đang phát câu: "${sentence}"`)

      const item: CockpitNowPlaying = {
        type: 'station',
        title: `Thông báo (${label}): ${event.stationName}`,
        text: sentence,
        time: formatClock(new Date()),
      }

      setNowPlaying(item)
      setIsPlaying(true)

      // Azure TTS Hoài My (Nữ miền Nam tự nhiên, chuẩn tiếng Việt Saigon Waterbus)
      void playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', 0.95, () => {
        setIsPlaying(false)
        setNowPlaying(null)
      })
    },
    [],
  )

  // Kết nối OnboardAudioHub để nhận sự kiện POI thuyết minh du lịch
  useEffect(() => {
    const unsub = connectHub(HUBS.onboardAudio, {
      handlers: {
        [HUB_EVENTS.audioTrigger]: (message: AudioTriggerMessage) => {
          const currentTrip = tripRef.current
          if (!currentTrip || currentTrip.status !== 'EnRoute') return
          if (!narratingRef.current) return
          if (message.tripId && message.tripId !== currentTrip.id) return
          if (message.boatId && currentTrip.boatId && message.boatId !== currentTrip.boatId) return
          void playPoiAudio(message)
        },
        [HUB_EVENTS.audioControl]: (control: AudioControlMessage) => {
          if (control.action === 'pause') {
            stopAll()
          }
        },
        [HUB_EVENTS.audioState]: () => {},
      },
      onConnected: async (connection) => {
        onboardHubConnectionRef.current = connection
      },
    })

    return () => {
      unsub()
      onboardHubConnectionRef.current = null
      stopAll()
    }
  }, [playPoiAudio, stopAll])

  // Lắng nghe StopAnnouncement trên BoatTrackingHub & tự động JoinBoat khi boatId có/thay đổi
  useEffect(() => {
    const unsub = connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.stopAnnouncement]: handleStopAnnouncement,
        [HUB_EVENTS.boatPosition]: () => {},
        [HUB_EVENTS.tripStatusChanged]: () => {},
      },
      onConnected: async (connection) => {
        boatTrackingConnectionRef.current = connection
        const boatId = tripRef.current?.boatId
        if (boatId) {
          try {
            await connection.invoke('JoinBoat', boatId)
          } catch {}
        }
      },
    })

    return () => {
      unsub()
      boatTrackingConnectionRef.current = null
    }
  }, [handleStopAnnouncement])

  // Đảm bảo luôn gia nhập group Boat khi trip.boatId xuất hiện hoặc thay đổi
  useEffect(() => {
    const boatId = trip?.boatId
    const conn = boatTrackingConnectionRef.current
    if (conn && boatId && conn.state === 'Connected') {
      void conn.invoke('JoinBoat', boatId).catch(() => {})
    }
  }, [trip?.boatId])

  return { isPlaying, nowPlaying, stopAll, handleStopAnnouncement }
}
