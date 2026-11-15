import { useCallback, useEffect, useRef, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { HubConnection } from '@microsoft/signalr'
import { apiRequest } from '../../../services/apiClient'
import { playAzureAnnouncement, stopCurrentAudio } from '../services/ttsPlayback'
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

const TERMINAL_STATUSES = ['Completed', 'Cancelled', 'Terminated', 'Suspended']

/**
 * Hook quản lý phát âm thanh buồng lái tự động theo hành trình tàu.
 * - Thông báo trạm bến tiếng Việt Azure TTS (Rời bến, Sắp đến bến, Cập bến).
 * - Thuyết minh du lịch tiếng Việt (POI) nếu là chuyến Sightseeing.
 * - Phát trọn vẹn từng câu theo hàng đợi (Audio Queue), không bị cắt nửa chừng, không bị lồng tiếng.
 */
export function useCockpitAudio({ trip, narrating }: UseCockpitAudioOptions) {
  const [isPlaying, setIsPlaying] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<CockpitNowPlaying | null>(null)

  const currentAudioRef = useRef<HTMLAudioElement | null>(null)
  const onboardHubConnectionRef = useRef<HubConnection | null>(null)
  const lastAnnounceKeyRef = useRef<{ key: string; at: number } | null>(null)

  const queueRef = useRef<AudioTask[]>([])
  const isBusyRef = useRef<boolean>(false)
  const tripRef = useRef<AdminTrip | undefined>(trip)
  const narratingRef = useRef(narrating)
  const processQueueRef = useRef<() => void>(() => {})

  useEffect(() => {
    tripRef.current = trip
  }, [trip])

  useEffect(() => {
    narratingRef.current = narrating
  }, [narrating])

  const stopAll = useCallback(() => {
    queueRef.current = []
    isBusyRef.current = false
    stopCurrentAudio()
    const audio = currentAudioRef.current
    if (audio) {
      try {
        audio.pause()
        audio.currentTime = 0
        audio.src = ''
      } catch { /* ignore */ }
      currentAudioRef.current = null
    }
    setIsPlaying(false)
    setNowPlaying(null)
  }, [])

  useEffect(() => {
    if (!trip || !TERMINAL_STATUSES.includes(trip.status)) return
    const timer = window.setTimeout(() => stopAll(), 0)
    return () => window.clearTimeout(timer)
  }, [trip, stopAll])

  const processQueue = useCallback(async () => {
    if (isBusyRef.current) return
    const task = queueRef.current.shift()
    if (!task) {
      setIsPlaying(false)
      setNowPlaying(null)
      return
    }

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
      await new Promise((resolve) => window.setTimeout(resolve, 400))
      const current = tripRef.current
      if (!current || !TERMINAL_STATUSES.includes(current.status)) {
        processQueueRef.current()
      } else {
        setIsPlaying(false)
        setNowPlaying(null)
      }
    }
  }, [])

  useEffect(() => {
    processQueueRef.current = () => {
      void processQueue()
    }
  }, [processQueue])

  const handleStopAnnouncement = useCallback(
    (event: StopAnnouncementEvent) => {
      const currentTrip = tripRef.current
      if (event.tripId && currentTrip && event.tripId !== currentTrip.id) return
      if (event.boatId && currentTrip?.boatId && event.boatId !== currentTrip.boatId) return
      if (currentTrip && TERMINAL_STATUSES.includes(currentTrip.status)) return

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

      const stationTask: AudioTask = {
        id: eventKey,
        type: 'station',
        title: `Thông báo (${label}): ${event.stationName}`,
        text: sentence,
        run: async () => {
          await playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', 0.95)
        },
      }

      queueRef.current.push(stationTask)

      const narration = event.narration
      if (narratingRef.current && narration?.text) {
        const narrationTask: AudioTask = {
          id: `${eventKey}-narration`,
          type: 'poi',
          title: `Thuyết minh: ${narration.poiName}`,
          text: narration.text,
          run: async () => {
            await playAzureAnnouncement(narration.text, 'vi-VN-HoaiMyNeural', 0.95)
          },
        }
        queueRef.current.push(narrationTask)
      }

      void processQueue()
    },
    [processQueue],
  )

  const playPoiAudio = useCallback(
    async (message: AudioTriggerMessage) => {
      if (!narratingRef.current) return

      const task: AudioTask = {
        id: message.requestId,
        type: 'poi',
        title: `Thuyết minh: ${message.poiName}`,
        text: `Đang phát thuyết minh danh lam thắng cảnh ${message.poiName}`,
        run: async () =>
          new Promise<void>((resolve) => {
            const finish = (status: 'Completed' | 'Failed') => {
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

            let blobUrl = ''

            const run = async () => {
              if (onboardHubConnectionRef.current) {
                try {
                  await onboardHubConnectionRef.current.invoke('AcceptPlayback', message.requestId)
                } catch { /* ignore */ }
              }

              const blob = await apiRequest<Blob>(message.playbackPath, { responseType: 'blob' })
              blobUrl = URL.createObjectURL(blob)

              if (onboardHubConnectionRef.current) {
                try {
                  await onboardHubConnectionRef.current.invoke(
                    'ReportPlaybackState',
                    message.requestId,
                    'Started',
                    null,
                  )
                } catch { /* ignore */ }
              }

              const audio = new Audio(blobUrl)
              audio.volume = 0.95
              currentAudioRef.current = audio

              audio.onended = () => finish('Completed')
              audio.onerror = () => finish('Failed')

              await audio.play()
            }

            run().catch((err) => {
              console.error('[Onboard Audio] Lỗi phát thuyết minh POI:', err)
              finish('Failed')
            })
          }),
      }

      queueRef.current.push(task)
      void processQueue()
    },
    [processQueue],
  )

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
