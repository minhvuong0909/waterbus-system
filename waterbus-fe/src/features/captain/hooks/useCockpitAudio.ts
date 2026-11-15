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

interface AudioTask {
  id: string
  type: 'station' | 'poi'
  title: string
  text: string
  run: () => Promise<void>
}

/**
 * Hook quản lý loa phát thanh buồng lái tự động theo hành trình tàu.
 * Chạy ngầm trong trang Captain:
 * - Khi Captain ấn [Bắt đầu chuyến] (trip chuyển sang EnRoute), hệ thống tự động:
 *   + Phát âm thanh tiếng Việt thông báo trạm bến (rời bến, sắp đến bến, cập bến).
 *   + Tự động phát thuyết minh du lịch tiếng Việt khi qua các điểm danh lam thắng cảnh (nếu bật thuyết minh).
 * - Sử dụng Hàng đợi âm thanh tuần tự (Audio Queue): Phát trọn vẹn từng câu, không ngắt ngang nửa chừng, không lồng tiếng.
 * - Khi chuyến kết thúc hoặc tạm dừng, tự động tắt loa.
 */
export function useCockpitAudio({ trip, narrating }: UseCockpitAudioOptions) {
  const [isPlaying, setIsPlaying] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<CockpitNowPlaying | null>(null)

  const currentAudioRef = useRef<HTMLAudioElement | null>(null)
  const onboardHubConnectionRef = useRef<HubConnection | null>(null)
  const lastAnnouncementRef = useRef<{ key: string; at: number } | null>(null)

  // Hàng đợi phát thanh tuần tự
  const queueRef = useRef<AudioTask[]>([])
  const isPlayingTaskRef = useRef<boolean>(false)

  const tripRef = useRef(trip)
  useEffect(() => {
    tripRef.current = trip
    // Nếu chuyến không ở trạng thái EnRoute, dừng toàn bộ âm thanh và xóa sạch hàng đợi
    if (trip?.status !== 'EnRoute') {
      queueRef.current = []
      isPlayingTaskRef.current = false
      stopCurrentAudio()
      if (currentAudioRef.current) {
        try {
          currentAudioRef.current.pause()
          currentAudioRef.current.currentTime = 0
        } catch {}
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
    queueRef.current = []
    isPlayingTaskRef.current = false
    stopCurrentAudio()
    if (currentAudioRef.current) {
      try {
        currentAudioRef.current.pause()
        currentAudioRef.current.currentTime = 0
      } catch {}
      currentAudioRef.current = null
    }
    setIsPlaying(false)
    setNowPlaying(null)
  }, [])

  // Bộ xử lý hàng đợi tuần tự: câu trước phát xong trọn vẹn 100% -> nghỉ 400ms -> phát câu tiếp theo
  const processQueue = useCallback(async () => {
    if (isPlayingTaskRef.current) return
    if (queueRef.current.length === 0) {
      setIsPlaying(false)
      setNowPlaying(null)
      return
    }

    const task = queueRef.current.shift()!
    isPlayingTaskRef.current = true
    setIsPlaying(true)
    setNowPlaying({
      type: task.type,
      title: task.title,
      text: task.text,
      time: formatClock(new Date()),
    })

    try {
      await task.run()
    } catch (e) {
      console.warn('[Cockpit Audio] Lỗi khi phát âm thanh:', e)
    } finally {
      isPlayingTaskRef.current = false
      // Nghỉ 400ms giữa các câu để người nghe cảm giác tự nhiên, thư thái
      await new Promise((resolve) => setTimeout(resolve, 400))
      // Tự động phát câu tiếp theo nếu còn trong hàng đợi
      if (tripRef.current?.status === 'EnRoute') {
        void processQueue()
      } else {
        setIsPlaying(false)
        setNowPlaying(null)
      }
    }
  }, [])

  // Phát audio file MP3 (thuyết minh du lịch POI)
  const playPoiAudio = useCallback(
    async (message: AudioTriggerMessage) => {
      // Nếu đang có thông báo trạm bến trong hàng đợi hoặc đang phát thì bỏ qua POI (an toàn hàng hải ưu tiên hơn)
      if (
        isPlayingTaskRef.current &&
        nowPlaying?.type === 'station'
      ) {
        console.log(`[Cockpit Audio] Đang phát thông báo trạm bến, nhường loa bỏ qua POI: ${message.poiName}`)
        return
      }

      const poiTask: AudioTask = {
        id: message.requestId,
        type: 'poi',
        title: `Thuyết minh: ${message.poiName}`,
        text: `Đang phát thuyết minh danh lam thắng cảnh ${message.poiName}`,
        run: async () => {
          return new Promise<void>(async (resolve) => {
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

              const cleanUp = () => {
                URL.revokeObjectURL(blobUrl)
                currentAudioRef.current = null
                resolve()
              }

              audio.onended = () => {
                if (onboardHubConnectionRef.current) {
                  void onboardHubConnectionRef.current.invoke(
                    'ReportPlaybackState',
                    message.requestId,
                    'Completed',
                    null,
                  )
                }
                cleanUp()
              }

              audio.onerror = () => {
                if (onboardHubConnectionRef.current) {
                  void onboardHubConnectionRef.current.invoke(
                    'ReportPlaybackState',
                    message.requestId,
                    'Failed',
                    'Lỗi phát file âm thanh',
                  )
                }
                cleanUp()
              }

              await audio.play()
            } catch (err) {
              console.error('[Onboard Audio] Lỗi tải/phát MP3:', err)
              resolve()
            }
          })
        },
      }

      // Giới hạn hàng đợi: POI không xếp quá 1 bài
      queueRef.current = queueRef.current.filter((t) => t.type !== 'poi')
      queueRef.current.push(poiTask)
      void processQueue()
    },
    [nowPlaying, processQueue],
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

      // Khử trùng sự kiện: cùng loại sự kiện của cùng bến trong vòng 5 giây -> bỏ qua
      const eventKey = `${event.tripId}-${event.type ?? event.eventType}-${event.stationName}`
      const now = Date.now()
      if (lastAnnouncementRef.current?.key === eventKey && now - lastAnnouncementRef.current.at < 5000) {
        console.log(`[Cockpit Audio] Bỏ qua sự kiện trạm trùng lặp: ${eventKey}`)
        return
      }
      lastAnnouncementRef.current = { key: eventKey, at: now }

      const sentence = getAnnouncementSentence(event)
      const label =
        event.type === 'departing'
          ? 'Rời bến'
          : event.type === 'near'
            ? 'Sắp đến bến'
            : 'Cập bến'

      console.log(`[Cockpit Audio] Nhận sự kiện (${label}): ${event.stationName} -> Xếp hàng phát: "${sentence}"`)

      const stationTask: AudioTask = {
        id: eventKey,
        type: 'station',
        title: `Thông báo (${label}): ${event.stationName}`,
        text: sentence,
        run: async () => {
          // Azure TTS Hoài My (Nữ miền Nam tự nhiên, chuẩn tiếng Việt Saigon Waterbus)
          // Đảm bảo phát trọn vẹn 100% câu thông báo trước khi chuyển câu khác
          await playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', 0.95)
        },
      }

      // Thông báo trạm bến có ưu tiên cao: loại bỏ các POI đang xếp hàng để ưu tiên đọc bến
      queueRef.current = queueRef.current.filter((t) => t.type !== 'poi')
      // Đưa câu thông báo bến vào hàng đợi
      queueRef.current.push(stationTask)
      void processQueue()
    },
    [processQueue],
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

  return { isPlaying, nowPlaying, stopAll, handleStopAnnouncement }
}
