import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { API_BASE_URL } from '../../../constant/env'
import { getToken } from '../../../services/authStorage'

/**
 * Downloads one clip of an announcement with the captain's token (the server gives no public URL) and returns an
 * object URL to play. The caller revokes it when the clip ends. It is the one place that needs a raw `fetch`: the
 * answer is a binary MP3, not JSON like the other API calls.
 */
export async function fetchClipObjectUrl(requestId: string, language: string): Promise<string> {
  const token = getToken()
  const response = await fetch(`${API_BASE_URL}${API_ENDPOINTS.onboardAudio.clip(requestId, language)}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  if (!response.ok) throw new Error(`Audio request failed (${response.status})`)
  return URL.createObjectURL(await response.blob())
}

/** Plays an audio URL to the end. Rejects when the browser blocks or cannot play it. */
export function playAudioUrl(url: string, onStart?: (stop: () => void) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const audio = new Audio(url)
    onStart?.(() => {
      audio.pause()
      resolve()
    })
    audio.onended = () => resolve()
    audio.onerror = () => reject(new Error('Cannot play audio'))
    audio.play().catch(reject)
  })
}
