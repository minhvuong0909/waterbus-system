import { useCallback, useEffect, useRef, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { HubConnection } from '@microsoft/signalr'
import { apiRequest } from '../../../services/apiClient'
import {
  playAzureAnnouncement,
  stopCurrentAudio,
} from '../../admin/services/simulatorService'
import { POI_SUGGESTIONS } from '../../admin/utils/audioGuide'
import type { StopAnnouncementEvent } from '../../operations/hooks/useFleetHub'
import type { AdminTrip } from '../../operations/types'
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

export interface AudioStateMessage {
  boatId: string
  tripId?: string
  tripStatus?: string
  tripType?: string
  playbackAllowed: boolean
  reason: string
}

export interface AnnouncementItem {
  id: string
  type: 'station' | 'poi' | 'test'
  title: string
  text: string
  time: string
}

interface CaptainSpeakerWidgetProps {
  trip?: AdminTrip
  narrating: boolean
  onToggleNarrating?: () => void
}

export function getAnnouncementSentence(event: StopAnnouncementEvent): string {
  const eventType = event.type || event.eventType || 'departing'
  let customTexts: Record<string, string> = {}
  try {
    const saved = localStorage.getItem('waterbus_announcement_custom_texts')
    if (saved) customTexts = JSON.parse(saved)
  } catch {}

  const station = event.stationName || 'Bến tàu'
  const nextStation = event.nextStationName || ''

  if (eventType === 'departing') {
    const raw =
      customTexts['departing'] ||
      'Tàu đang rời bến {Bến hiện tại}, điểm đến tiếp theo: Bến {Bến kế tiếp}.'
    return raw
      .replace(/{Bến hiện tại}/g, station)
      .replace(/{Bến kế tiếp}/g, nextStation)
  }

  if (eventType === 'near') {
    const raw =
      customTexts['near'] ||
      'Tàu sắp đến bến {Bến kế tiếp}. Quý khách vui lòng chuẩn bị tư trang hành lý.'
    return raw
      .replace(/{Bến kế tiếp}/g, station)
      .replace(/{Bến hiện tại}/g, station)
  }

  // arrived
  const raw =
    customTexts['arrived_mid'] ||
    'Tàu đã đến bến {Bến hiện tại}. Tàu sẽ dừng 5 giây để quý khách lên xuống.'
  return raw.replace(/{Bến hiện tại}/g, station).replace(/{Bến cuối}/g, station)
}

export default function CaptainSpeakerWidget({
  trip,
  narrating,
  onToggleNarrating,
}: CaptainSpeakerWidgetProps) {
  const [volume, setVolume] = useState<number>(() => {
    try {
      const v = localStorage.getItem('waterbus_cockpit_volume')
      return v ? parseFloat(v) : 0.85
    } catch {
      return 0.85
    }
  })
  const [isMuted, setIsMuted] = useState(false)
  const [isPlaying, setIsPlaying] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<AnnouncementItem | null>(null)
  const [history, setHistory] = useState<AnnouncementItem[]>([])
  const [hubConnected, setHubConnected] = useState(false)
  const [selectedDemoPoi, setSelectedDemoPoi] = useState<string>('cầu ba son')

  const currentAudioRef = useRef<HTMLAudioElement | null>(null)
  const onboardHubConnectionRef = useRef<HubConnection | null>(null)

  const tripRef = useRef(trip)
  useEffect(() => {
    tripRef.current = trip
  }, [trip])

  const narratingRef = useRef(narrating)
  useEffect(() => {
    narratingRef.current = narrating
  }, [narrating])

  const volumeRef = useRef(volume)
  useEffect(() => {
    volumeRef.current = volume
    try {
      localStorage.setItem('waterbus_cockpit_volume', String(volume))
    } catch {}
  }, [volume])

  const isMutedRef = useRef(isMuted)
  useEffect(() => {
    isMutedRef.current = isMuted
    if (currentAudioRef.current) {
      currentAudioRef.current.volume = isMuted ? 0 : volume
    }
  }, [isMuted, volume])

  const addHistory = useCallback((item: AnnouncementItem) => {
    setHistory((prev) => [item, ...prev.slice(0, 7)])
  }, [])

  // Dừng mọi âm thanh đang phát
  const handleStopAll = useCallback(() => {
    stopCurrentAudio()
    if (currentAudioRef.current) {
      currentAudioRef.current.pause()
      currentAudioRef.current.currentTime = 0
      currentAudioRef.current = null
    }
    setIsPlaying(false)
    setNowPlaying(null)
  }, [])

  // Phát audio file MP3 từ URL với Bearer Token
  const playPoiAudio = useCallback(
    async (message: AudioTriggerMessage) => {
      handleStopAll()
      const effectiveVol = isMutedRef.current ? 0 : volumeRef.current
      const item: AnnouncementItem = {
        id: message.requestId,
        type: 'poi',
        title: `Thuyết minh du lịch: ${message.poiName}`,
        text: `Đang phát thuyết minh danh lam thắng cảnh ${message.poiName}`,
        time: formatClock(new Date()),
      }

      setNowPlaying(item)
      setIsPlaying(true)
      addHistory(item)

      try {
        // Gửi ACK tới hub nếu có kết nối
        if (onboardHubConnectionRef.current) {
          try {
            await onboardHubConnectionRef.current.invoke('AcceptPlayback', message.requestId)
          } catch {}
        }

        // Tải audio blob có JWT
        const blob = await apiRequest<Blob>(message.playbackPath, { responseType: 'blob' })
        const blobUrl = URL.createObjectURL(blob)

        // Báo Started
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
        audio.volume = effectiveVol
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
    [handleStopAll, addHistory],
  )

  // Phát thông báo trạm bến
  const handlePlayStationAnnouncement = useCallback(
    (event: StopAnnouncementEvent) => {
      const sentence = getAnnouncementSentence(event)
      const label =
        event.type === 'departing'
          ? 'Rời bến'
          : event.type === 'near'
            ? 'Sắp đến bến'
            : 'Cập bến'

      const item: AnnouncementItem = {
        id: `station-${Date.now()}`,
        type: 'station',
        title: `Thông báo trạm bến (${label}): ${event.stationName}`,
        text: sentence,
        time: formatClock(new Date()),
      }

      setNowPlaying(item)
      setIsPlaying(true)
      addHistory(item)

      const effectiveVol = isMutedRef.current ? 0 : volumeRef.current
      void playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', effectiveVol, () => {
        setIsPlaying(false)
        setNowPlaying(null)
      })
    },
    [addHistory],
  )

  // Kết nối OnboardAudioHub để nhận sự kiện POI thuyết minh thời gian thực
  useEffect(() => {
    const unsub = connectHub(HUBS.onboardAudio, {
      handlers: {
        [HUB_EVENTS.audioTrigger]: (message: AudioTriggerMessage) => {
          if (!narratingRef.current) return
          void playPoiAudio(message)
        },
        [HUB_EVENTS.audioControl]: (control: AudioControlMessage) => {
          if (control.action === 'pause') {
            handleStopAll()
          }
        },
        [HUB_EVENTS.audioState]: () => {
          // Sync state if needed
        },
      },
      onConnected: async (connection) => {
        onboardHubConnectionRef.current = connection
        setHubConnected(true)
      },
      onStatus: (status) => {
        setHubConnected(status === 'live')
      },
    })

    return () => {
      unsub()
      onboardHubConnectionRef.current = null
      handleStopAll()
    }
  }, [playPoiAudio, handleStopAll])

  // Lắng nghe StopAnnouncement trên BoatTrackingHub
  useEffect(() => {
    const unsub = connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.stopAnnouncement]: (event: StopAnnouncementEvent) => {
          const currentTrip = tripRef.current
          if (event.boatId && currentTrip?.boatId && event.boatId !== currentTrip.boatId) {
            return
          }
          handlePlayStationAnnouncement(event)
        },
      },
      onConnected: async (connection) => {
        const boatId = tripRef.current?.boatId
        if (boatId) {
          await connection.invoke('JoinBoat', boatId)
        }
      },
    })

    return () => unsub()
  }, [handlePlayStationAnnouncement])

  // Thử loa phát thanh buồng lái (Test Speaker)
  const handleTestSpeaker = useCallback(() => {
    handleStopAll()
    const sentence =
      'Hệ thống loa phát thanh trên tàu đang hoạt động tốt. Âm thanh thông báo trạm bến và thuyết minh du lịch tự động đã sẵn sàng.'
    const item: AnnouncementItem = {
      id: `test-${Date.now()}`,
      type: 'test',
      title: 'Kiểm tra loa buồng lái',
      text: sentence,
      time: formatClock(new Date()),
    }
    setNowPlaying(item)
    setIsPlaying(true)
    addHistory(item)

    const effectiveVol = isMuted ? 0 : volume
    void playAzureAnnouncement(sentence, 'vi-VN-HoaiMyNeural', effectiveVol, () => {
      setIsPlaying(false)
      setNowPlaying(null)
    })
  }, [handleStopAll, isMuted, volume, addHistory])

  // Thử thuyết minh POI mẫu
  const handleTestPoiDemo = useCallback(() => {
    handleStopAll()
    const text = POI_SUGGESTIONS[selectedDemoPoi] || POI_SUGGESTIONS['cầu ba son']
    const poiLabel =
      selectedDemoPoi === 'cầu ba son'
        ? 'Cầu Ba Son'
        : selectedDemoPoi === 'landmark 81'
          ? 'Landmark 81'
          : selectedDemoPoi === 'cột cờ thủ ngữ'
            ? 'Cột cờ Thủ Ngữ & Bến Nhà Rồng'
            : selectedDemoPoi === 'thanh đa'
              ? 'Bán đảo Thanh Đa'
              : 'Cầu Bình Lợi'

    const item: AnnouncementItem = {
      id: `demo-${Date.now()}`,
      type: 'poi',
      title: `Thử thuyết minh mẫu: ${poiLabel}`,
      text,
      time: formatClock(new Date()),
    }
    setNowPlaying(item)
    setIsPlaying(true)
    addHistory(item)

    const effectiveVol = isMuted ? 0 : volume
    void playAzureAnnouncement(text, 'vi-VN-HoaiMyNeural', effectiveVol, () => {
      setIsPlaying(false)
      setNowPlaying(null)
    })
  }, [handleStopAll, selectedDemoPoi, isMuted, volume, addHistory])

  return (
    <div className="overflow-hidden rounded-2xl border border-outline-variant bg-surface shadow-xs">
      {/* Header Buồng lái */}
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-outline-variant/60 bg-surface-container-low px-5 py-3.5">
        <div className="flex items-center gap-2.5">
          <div
            className={`flex h-8 w-8 items-center justify-center rounded-lg ${
              isPlaying
                ? 'bg-primary text-on-primary animate-pulse'
                : 'bg-primary-container text-on-primary-container'
            }`}
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M15.536 8.464a5 5 0 010 7.072m2.828-9.9a9 9 0 010 12.728M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z"
              />
            </svg>
          </div>
          <div>
            <h3 className="text-sm font-bold uppercase tracking-wide text-on-surface">
              Hệ thống loa phát thanh buồng lái
            </h3>
            <div className="flex items-center gap-2 text-xs text-on-surface-variant">
              <span className="flex items-center gap-1">
                <span
                  className={`inline-block h-2 w-2 rounded-full ${
                    hubConnected ? 'bg-emerald-500' : 'bg-amber-500'
                  }`}
                />
                {hubConnected ? 'SignalR Audio: Sẵn sàng' : 'SignalR: Đang kết nối...'}
              </span>
              <span>•</span>
              <span>Tàu: <strong>{trip?.boatCode || 'Tàu buýt'}</strong></span>
            </div>
          </div>
        </div>

        {/* Trạng thái loa buồng lái & nút bật tắt */}
        <div className="flex items-center gap-2">
          {onToggleNarrating && (
            <button
              type="button"
              onClick={onToggleNarrating}
              className={`rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors ${
                narrating
                  ? 'bg-secondary-container text-on-secondary-container hover:bg-secondary-container/80'
                  : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high'
              }`}
            >
              {narrating ? '🎙️ Thuyết minh: BẬT' : '🎙️ Thuyết minh: TẮT'}
            </button>
          )}

          <button
            type="button"
            onClick={() => setIsMuted(!isMuted)}
            className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-semibold transition-colors ${
              isMuted
                ? 'border-red-300 bg-red-50 text-red-700 hover:bg-red-100'
                : 'border-outline-variant bg-surface text-on-surface hover:bg-surface-container'
            }`}
          >
            {isMuted ? (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2" />
                </svg>
                Tắt loa (Muted)
              </>
            ) : (
              <>
                <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.536 8.464a5 5 0 010 7.072M5.586 15H4a1 1 0 01-1-1v-4a1 1 0 011-1h1.586l4.707-4.707C10.923 3.663 12 4.109 12 5v14c0 .891-1.077 1.337-1.707.707L5.586 15z" />
                </svg>
                Loa đang mở
              </>
            )}
          </button>
        </div>
      </div>

      <div className="p-4 sm:p-5">
        {/* Banner đang phát thanh */}
        {isPlaying && nowPlaying ? (
          <div className="mb-4 rounded-xl border border-primary/30 bg-primary-container/20 p-4">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="flex h-3 w-3 relative">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-primary opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-3 w-3 bg-primary"></span>
                </span>
                <span className="text-xs font-bold uppercase tracking-wider text-primary">
                  {nowPlaying.type === 'station'
                    ? 'Đang phát thông báo trạm bến'
                    : nowPlaying.type === 'poi'
                      ? 'Đang phát thuyết minh du lịch'
                      : 'Đang kiểm tra loa buồng lái'}
                </span>
              </div>
              <button
                type="button"
                onClick={handleStopAll}
                className="rounded-md bg-red-100 px-2.5 py-1 text-xs font-semibold text-red-700 hover:bg-red-200 transition-colors"
              >
                Dừng phát
              </button>
            </div>

            <h4 className="mt-2 text-base font-bold text-on-surface">
              {nowPlaying.title}
            </h4>
            <p className="mt-1 text-sm text-on-surface-variant italic">
              "{nowPlaying.text}"
            </p>

            {/* Sóng âm Equalizer Animation */}
            <div className="mt-3 flex items-center gap-1">
              {[40, 75, 55, 90, 60, 85, 45, 95, 70, 50, 80, 65].map((h, i) => (
                <div
                  key={i}
                  className="w-1 rounded-full bg-primary animate-pulse"
                  style={{
                    height: `${(h * 0.22)}px`,
                    animationDuration: `${0.4 + (i % 4) * 0.2}s`,
                  }}
                />
              ))}
              <span className="ml-2 text-xs text-primary font-medium">Phát ra loa hành khách & buồng lái</span>
            </div>
          </div>
        ) : (
          <div className="mb-4 rounded-xl border border-outline-variant/60 bg-surface-container-low/50 p-3.5 flex items-center justify-between text-xs text-on-surface-variant">
            <div className="flex items-center gap-2">
              <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500"></span>
              <span>Loa buồng lái đang ở chế độ chờ phát tự động theo hành trình tàu.</span>
            </div>
            <span className="font-mono text-[11px] text-on-surface-variant/70">Azure TTS Hoài My • Realtime</span>
          </div>
        )}

        {/* Bảng điều khiển: Âm lượng & Thử loa nhanh */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 rounded-xl bg-surface-container-lowest p-4 border border-outline-variant/40">
          {/* Thanh chỉnh âm lượng */}
          <div className="flex flex-col justify-center">
            <div className="flex items-center justify-between text-xs font-semibold text-on-surface mb-1.5">
              <span className="flex items-center gap-1.5">
                <svg className="h-4 w-4 text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5L6 9H2v6h4l5 4V5z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19.07 4.93a10 10 0 010 14.14M15.54 8.46a5 5 0 010 7.08" />
                </svg>
                Âm lượng loa: {Math.round(volume * 100)}%
              </span>
              {isMuted && <span className="text-red-500 font-bold">(Đang tắt)</span>}
            </div>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={volume}
              onChange={(e) => {
                setVolume(parseFloat(e.target.value))
                if (isMuted) setIsMuted(false)
              }}
              className="h-2 w-full cursor-pointer appearance-none rounded-lg bg-surface-container-high accent-primary"
            />
          </div>

          {/* Nút bấm kiểm tra & thử loa */}
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={handleTestSpeaker}
              disabled={isPlaying}
              className="flex-1 flex items-center justify-center gap-1.5 rounded-xl bg-primary px-3 py-2.5 text-xs font-bold text-on-primary shadow-xs hover:bg-primary/90 transition-colors disabled:opacity-50"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z" />
              </svg>
              🔊 Thử loa buồng lái
            </button>

            <button
              type="button"
              onClick={handleTestPoiDemo}
              disabled={isPlaying}
              className="flex-1 flex items-center justify-center gap-1.5 rounded-xl border border-primary/30 bg-primary-container/30 px-3 py-2.5 text-xs font-bold text-primary hover:bg-primary-container/50 transition-colors disabled:opacity-50"
            >
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 100-6 3 3 0 000 6z" />
              </svg>
              🎙️ Thử thuyết minh mẫu
            </button>
          </div>
        </div>

        {/* Lựa chọn điểm du lịch demo */}
        <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-on-surface-variant">
          <span className="font-semibold">Điểm thuyết minh thử:</span>
          {[
            { id: 'cầu ba son', label: 'Cầu Ba Son' },
            { id: 'landmark 81', label: 'Landmark 81' },
            { id: 'cột cờ thủ ngữ', label: 'Bến Nhà Rồng' },
            { id: 'thanh đa', label: 'Bán đảo Thanh Đa' },
            { id: 'bình lợi', label: 'Cầu Bình Lợi' },
          ].map((poi) => (
            <button
              key={poi.id}
              type="button"
              onClick={() => setSelectedDemoPoi(poi.id)}
              className={`rounded-lg px-2.5 py-1 text-xs transition-colors ${
                selectedDemoPoi === poi.id
                  ? 'bg-secondary text-on-secondary font-bold'
                  : 'bg-surface-container text-on-surface-variant hover:bg-surface-container-high'
              }`}
            >
              {poi.label}
            </button>
          ))}
        </div>

        {/* Lịch sử phát thanh gần nhất */}
        {history.length > 0 && (
          <div className="mt-4 border-t border-outline-variant/60 pt-3">
            <div className="text-xs font-bold uppercase tracking-wider text-on-surface-variant mb-2">
              Lịch sử phát thanh gần đây
            </div>
            <div className="space-y-1.5">
              {history.map((item) => (
                <div
                  key={item.id}
                  className="flex items-start justify-between gap-2 rounded-lg bg-surface-container-low px-3 py-2 text-xs"
                >
                  <div className="flex-1">
                    <div className="flex items-center gap-1.5">
                      <span
                        className={`inline-block h-1.5 w-1.5 rounded-full ${
                          item.type === 'station'
                            ? 'bg-blue-500'
                            : item.type === 'poi'
                              ? 'bg-emerald-500'
                              : 'bg-amber-500'
                        }`}
                      />
                      <span className="font-bold text-on-surface">{item.title}</span>
                    </div>
                    <div className="mt-0.5 line-clamp-1 text-on-surface-variant">
                      {item.text}
                    </div>
                  </div>
                  <span className="font-mono text-[11px] text-on-surface-variant/70 shrink-0">
                    {item.time}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
