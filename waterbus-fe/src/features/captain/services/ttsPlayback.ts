import { generateNarration, fetchNarrationAudio } from '../../admin/services/audioGuideService'
import type { NarrationVoice } from '../../admin/utils/audioGuide'

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
    } catch { /* ignore */ }
    currentAudio = null
  }
  if ('speechSynthesis' in window) {
    try {
      window.speechSynthesis.cancel()
    } catch { /* ignore */ }
  }
}

/** Dừng âm thanh đang phát (cả Azure Audio lẫn SpeechSynthesis fallback). */
export function stopCurrentAudio(): void {
  currentAudioEpoch++
  pauseCurrentMedia()
}

function clampVolume(volume: number): number {
  return Math.max(0, Math.min(1, volume))
}

function speakWithBrowser(text: string, volume: number, epoch: number): Promise<void> {
  return new Promise<void>((resolve) => {
    if (!('speechSynthesis' in window)) {
      resolve()
      return
    }
    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.volume = clampVolume(volume)
    const voices = window.speechSynthesis.getVoices()
    const viVoice = voices.find((v) => v.lang.startsWith('vi') || v.name.includes('Vietnamese'))
    if (viVoice) utterance.voice = viVoice
    utterance.lang = 'vi-VN'
    const finish = () => {
      if (epoch === currentAudioEpoch) resolve()
    }
    utterance.onend = finish
    utterance.onerror = finish
    window.speechSynthesis.speak(utterance)
  })
}

function playAudioUrl(audioUrl: string, volume: number): Promise<void> {
  return new Promise<void>((resolve) => {
    pauseCurrentMedia()
    const audio = new Audio(audioUrl)
    audio.volume = clampVolume(volume)
    currentAudio = audio
    const finish = () => {
      if (currentAudio === audio) currentAudio = null
      resolve()
    }
    audio.onended = finish
    audio.onerror = finish
    audio.play().catch(() => finish())
  })
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
  try {
    const now = Date.now()
    if (text === lastSpokenText && now - lastSpokenTime < 3500) {
      console.log(`[TTS] Bỏ qua câu phát trùng lặp: "${text}"`)
      return
    }
    lastSpokenText = text
    lastSpokenTime = now

    stopCurrentAudio()
    const epoch = currentAudioEpoch

    let audioUrl = ttsCache.get(text)
    if (!audioUrl) {
      const generated = await generateNarration(text, voice)
      if (epoch !== currentAudioEpoch) return

      const blob = await fetchNarrationAudio(generated.assetKey)
      if (epoch !== currentAudioEpoch) return

      audioUrl = URL.createObjectURL(blob)
      ttsCache.set(text, audioUrl)
    }

    if (epoch !== currentAudioEpoch) return

    await playAudioUrl(audioUrl, volume)
  } catch (error) {
    console.warn('[TTS] Lỗi phát Azure TTS, fallback sang Web Speech nếu khả dụng', error)
    await speakWithBrowser(text, volume, currentAudioEpoch)
  } finally {
    onEnded?.()
  }
}
