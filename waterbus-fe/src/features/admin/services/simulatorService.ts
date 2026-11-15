import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { fetchNarrationAudio, generateNarration } from './audioGuideService'
import type { NarrationVoice } from '../utils/audioGuide'

export interface StopAnnouncementConfig {
  segmentDurationSeconds: number
  dwellTimeSeconds: number
  nearThreshold: number
  useSimulatedSegmentDuration: boolean
}

/** Admin: lấy cấu hình thông báo bến và mô phỏng hành trình hiện tại. */
export async function getSimulatorConfig(): Promise<StopAnnouncementConfig> {
  return apiRequest<StopAnnouncementConfig>(API_ENDPOINTS.admin.simulatorStopAnnouncement)
}

/** Admin: cập nhật cấu hình thông báo bến và tốc độ mô phỏng hành trình. */
export async function updateSimulatorConfig(payload: StopAnnouncementConfig): Promise<StopAnnouncementConfig> {
  return apiRequest<StopAnnouncementConfig>(API_ENDPOINTS.admin.simulatorStopAnnouncement, {
    method: 'PUT',
    body: payload,
  })
}

const ttsCache = new Map<string, string>()
let currentAudio: HTMLAudioElement | null = null

/** Dừng âm thanh đang phát (cả Azure Audio lẫn SpeechSynthesis fallback). */
export function stopCurrentAudio(): void {
  if (currentAudio) {
    currentAudio.pause()
    currentAudio.currentTime = 0
    currentAudio = null
  }
  if ('speechSynthesis' in window) {
    window.speechSynthesis.cancel()
  }
}

/**
 * Phát âm thanh giọng đọc tiếng Việt Azure Neural Voice (Hoài My / Nam Minh) từ Azure Speech API.
 * Có bộ nhớ đệm (cache) để các câu thông báo bến lặp lại phát tức thì.
 */
export async function playAzureAnnouncement(
  text: string,
  voice: NarrationVoice = 'vi-VN-HoaiMyNeural',
  volume = 1,
  onEnded?: () => void,
): Promise<void> {
  try {
    stopCurrentAudio()

    let audioUrl = ttsCache.get(text)
    if (!audioUrl) {
      const generated = await generateNarration(text, voice)
      const blob = await fetchNarrationAudio(generated.assetKey)
      audioUrl = URL.createObjectURL(blob)
      ttsCache.set(text, audioUrl)
    }

    const audio = new Audio(audioUrl)
    audio.volume = Math.max(0, Math.min(1, volume))
    currentAudio = audio

    audio.onended = () => {
      if (currentAudio === audio) currentAudio = null
      onEnded?.()
    }
    audio.onerror = () => {
      if (currentAudio === audio) currentAudio = null
      onEnded?.()
    }

    await audio.play()
  } catch (error) {
    console.warn('[TTS] Lỗi phát Azure TTS, fallback sang Web Speech nếu khả dụng', error)
    if ('speechSynthesis' in window) {
      window.speechSynthesis.cancel()
      const u = new SpeechSynthesisUtterance(text)
      u.volume = Math.max(0, Math.min(1, volume))
      const voices = window.speechSynthesis.getVoices()
      const viVoice = voices.find((v) => v.lang.startsWith('vi') || v.name.includes('Vietnamese'))
      if (viVoice) u.voice = viVoice
      u.lang = 'vi-VN'
      u.onend = () => onEnded?.()
      u.onerror = () => onEnded?.()
      window.speechSynthesis.speak(u)
    } else {
      onEnded?.()
    }
  }
}
