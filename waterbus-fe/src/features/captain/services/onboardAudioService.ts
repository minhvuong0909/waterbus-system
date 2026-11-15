import { API_BASE_URL } from '../../../constant/env'
import { getToken } from '../../../services/authStorage'

/**
 * Downloads one clip with the captain's token (the server gives no public URL) and returns an object URL to play.
 * The caller revokes it when the clip ends.
 */
export async function fetchClipObjectUrl(playbackPath: string): Promise<string> {
  const token = getToken()
  const response = await fetch(`${API_BASE_URL}${playbackPath}`, {
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
