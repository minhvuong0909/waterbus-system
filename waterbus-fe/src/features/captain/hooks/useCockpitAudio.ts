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

interface AudioTask {
  id: string
  type: 'station' | 'poi'
  title: string
  text: string
  run: () => Promise<void>
}

interface UseCockpitAudioOptions {
  trip?: AdminTrip
  narrating: boolean
}

/**
 * Hook quản lý phát âm thanh buồng lái tự động theo hành trình tàu.
 * - Thông báo trạm bến tiếng Việt Azure TTS (Rời bến, Sắp đến bến, Cập bến).
 * - Thuyết minh du lịch tiếng Việt (POI MP3) nếu là chuyến Sightseeing.
 * - Phát trọn vẹn từng câu theo hàng đợi (Audio Queue), không bị cắt nửa chừng, không bị lồng tiếng.
 */
export function useCockpitAudio({ trip, narrating }: UseCockpitAudioOptions) {
  const [isPlaying, setIsPlaying] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<CockpitNowPlaying | null>(null)

  const currentAudioRef = useRef<HTMLAudioElement | null>(null)
  const onboardHubConnectionRef = useRef<HubConnection | null>(null)
  const lastAnnounceKeyRef = useRef<{ key: string; at: number } | null>(null)

  // Hàng đợi phát âm thanh tuần tự
  const queueRef = useRef<AudioTask[]>([])
  const isBusyRef = useRef<boolean>(false)

  const tripRef = useRef(trip)
  tripRef.current = trip

  const narratingRef = useRef(narrating)
  narratingRef.current = narrating

  // Dừng phát toàn bộ âm thanh
  const stopAll = useCallback(() => {
    queueRef.current = []
    isBusyRef.current = false
    stopCurrentAudio()
    if (currentAudioRef.current) {
      try {
        currentAudioRef.current.pause()
        currentAudioRef.current.currentTime = 0
        currentAudioRef.current.src = ''
      } catch {}
      currentAudioRef.current = null
    }
    setIsPlaying(false)
    setNowPlaying(null)
  }, [])

  // Khi chuyến tàu kết thúc hoặc tạm dừng thì tắt loa
  useEffect(() => {
    const terminalStatuses = ['Completed', 'Cancelled', 'Terminated', 'Suspended']
    if (trip && terminalStatuses.includes(trip.status)) {
      stopAll()
    }
  }, [trip, stopAll])

  // Bộ điều phối hàng đợi âm thanh: câu này phát xong trọn vẹn 100% mới phát tiếp câu sau
  const processQueue = useCallback(async () => {
    if (isBusyRef.current) return
    if (queueRef.current.length === 0) {
      setIsPlaying(false)
      setNowPlaying(null)
      return
    }

    const task = queueRef.current.shift()!
    isBusyRef.current = true
    setIsPlaying(true)
    setNowPlaying({
      type: task.type,
      title: task.title,
      text: task.text,
      time: formatClock(new Date()),
    })

    try {
      await task.run()
    } catch (err) {
      console.warn('[Cockpit Audio] Lỗi phát âm thanh:', err)
    } finally {
      isBusyRef.current = false
      // Khoảng nghỉ nhỏ 400ms giữa các câu để người nghe cảm giác tự nhiên
      await new Promise((r) => setTimeout(r, 400))
      // Tự động phát câu tiếp theo nếu còn trong hàng đợi
      const terminalStatuses = ['Completed', 'Cancelled', 'Terminated', 'Suspended']
      if (!tripRef.current || !terminalStatuses.includes(tripRef.current.status)) {
        void processQueue()
      } else {
        setIsPlaying(false)
        setNowPlaying(null)
      }
    }
  }, [])

  // Xử lý thông báo trạm bến (Azure TTS Hoài My)
  const handleStopAnnouncement = useCallback(
    (event: StopAnnouncementEvent) => {
      const currentTrip = tripRef.current
      // Lọc theo chuyến đang chọn nếu có id
      if (event.tripId && currentTrip && event.tripId !== currentTrip.id) return
      if (event.boatId && currentTrip?.boatId && event.boatId !== currentTrip.boatId) return

      // Bỏ qua nếu chuyến đã kết thúc hẳn
      const terminalStatuses = ['Completed', 'Cancelled', 'Terminated', 'Suspended']
      if (currentTrip && terminalStatuses.includes(currentTrip.status)) return

      // Khử trùng: không phát lại cùng 1 sự kiện của cùng bến trong vòng 4 giây
      const eventKey = `${event.tripId}-${event.type ?? event.eventType}-${event.stationName}`
      const now = Date.now()
      if (lastAnnounceKeyRef.current?.key === eventKey && now - lastAnnounceKeyRef.current.at < 4000) {
        return
      }
      lastAnnounceKeyRef.current = { key: eventKey, at: now }

      const sentence = getAnnouncementSentence(event)
      const label =
        event.type === 'departing'
          ? 'Rời bến'
          : event.type === 'near'
            ? 'Sắp đến bến'
            : 'Cập bến'

      console.log(`[Cockpit Audio] Nhận sự kiện (${label}): ${event.stationName} -> Đang phát: "${sentence}"`)

      const task: AudioTask = {
        id: eventKey,
        type: 'station',
        title: `Thông báo (${label}): ${event.stationName}`,
        text: sentence,
        run: async () => {
          // Phát trọn vẹn câu thông báo bằng giọng đọc Azure TTS tiếng Việt
          await playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', 0.95)
        },
      }

      // Thông báo trạm bến ưu tiên hơn POI: dọn bớt POI trong hàng đợi
      queueRef.current = queueRef.current.filter((t) => t.type !== 'poi')
      queueRef.current.push(task)
      void processQueue()
    },
    [processQueue],
  )

  // Xử lý phát thuyết minh du lịch POI (MP3)
  const playPoiAudio = useCallback(
    async (message: AudioTriggerMessage) => {
      // Chỉ phát khi bật thuyết minh và không có thông báo trạm bến đang chờ
      if (!narratingRef.current) return
      if (queueRef.current.some((t) => t.type === 'station') || (isBusyRef.current && nowPlaying?.type === 'station')) {
        return
      }

      const task: AudioTask = {
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

              const done = (status: 'Completed' | 'Failed') => {
                URL.revokeObjectURL(blobUrl)
                currentAudioRef.current = null
                if (onboardHubConnectionRef.current) {
                  void onboardHubConnectionRef.current.invoke(
                    'ReportPlaybackState',
                    message.requestId,
                    status,
                    null,
                  )
                }
                resolve()
              }

              audio.onended = () => done('Completed')
              audio.onerror = () => done('Failed')

              await audio.play()
            } catch (err) {
              console.error('[Onboard Audio] Lỗi phát thuyết minh POI:', err)
              resolve()
            }
          })
        },
      }

      queueRef.current = queueRef.current.filter((t) => t.type !== 'poi')
      queueRef.current.push(task)
      void processQueue()
    },
    [nowPlaying, processQueue],
  )

  // Kết nối OnboardAudioHub để nhận sự kiện POI thuyết minh du lịch
  useEffect(() => {
    const unsub = connectHub(HUBS.onboardAudio, {
      handlers: {
        [HUB_EVENTS.audioTrigger]: (message: AudioTriggerMessage) => {
          const currentTrip = tripRef.current
          if (!currentTrip) return
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
