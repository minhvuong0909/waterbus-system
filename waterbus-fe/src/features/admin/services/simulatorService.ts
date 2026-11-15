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
let currentAudioEpoch = 0
let lastSpokenText = ''
let lastSpokenTime = 0

function pauseCurrentMedia(): void {
  if (currentAudio) {
    try {
      currentAudio.pause()
      currentAudio.currentTime = 0
      currentAudio.src = ''
    } catch {}
    currentAudio = null
  }
  if ('speechSynthesis' in window) {
    try {
      window.speechSynthesis.cancel()
    } catch {}
  }
}

/** Dừng âm thanh đang phát (cả Azure Audio lẫn SpeechSynthesis fallback). */
export function stopCurrentAudio(): void {
  currentAudioEpoch++
  pauseCurrentMedia()
}

/**
 * Phát âm thanh giọng đọc tiếng Việt Azure Neural Voice (Hoài My / Nam Minh) từ Azure Speech API.
 * Có bộ nhớ đệm (cache) để các câu thông báo bến lặp lại phát tức thì.
 * Đảm bảo chỉ 1 âm thanh duy nhất được phát tại một thời điểm (chống race condition & 2 loa phát cùng lúc).
 */
export async function playAzureAnnouncement(
  text: string,
  voice: NarrationVoice = 'vi-VN-HoaiMyNeural',
  volume = 1,
  onEnded?: () => void,
): Promise<void> {
  const now = Date.now()
  // Khử trùng: nếu cùng một nội dung được yêu cầu phát lại trong vòng 3.5 giây -> bỏ qua
  if (text === lastSpokenText && now - lastSpokenTime < 3500) {
    console.log(`[TTS] Bỏ qua câu phát trùng lặp: "${text}"`)
    return
  }
  lastSpokenText = text
  lastSpokenTime = now

  // Đánh dấu phiên phát mới, dừng ngay âm thanh cũ
  stopCurrentAudio()
  const thisEpoch = currentAudioEpoch

  try {
    let audioUrl = ttsCache.get(text)
    if (!audioUrl) {
      const generated = await generateNarration(text, voice)
      if (thisEpoch !== currentAudioEpoch) return

      const blob = await fetchNarrationAudio(generated.assetKey)
      if (thisEpoch !== currentAudioEpoch) return

      audioUrl = URL.createObjectURL(blob)
      ttsCache.set(text, audioUrl)
    }

    if (thisEpoch !== currentAudioEpoch) return

    pauseCurrentMedia()
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
    if (thisEpoch !== currentAudioEpoch) return
    console.warn('[TTS] Lỗi phát Azure TTS, fallback sang Web Speech nếu khả dụng', error)
    if ('speechSynthesis' in window) {
      window.speechSynthesis.cancel()
      const u = new SpeechSynthesisUtterance(text)
      u.volume = Math.max(0, Math.min(1, volume))
      const voices = window.speechSynthesis.getVoices()
      const viVoice = voices.find((v) => v.lang.startsWith('vi') || v.name.includes('Vietnamese'))
      if (viVoice) u.voice = viVoice
      u.lang = 'vi-VN'
      u.onend = () => {
        if (thisEpoch === currentAudioEpoch) onEnded?.()
      }
      u.onerror = () => {
        if (thisEpoch === currentAudioEpoch) onEnded?.()
      }
      window.speechSynthesis.speak(u)
    } else {
      onEnded?.()
    }
  }
}
