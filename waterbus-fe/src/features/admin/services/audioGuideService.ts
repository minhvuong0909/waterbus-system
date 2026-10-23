import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { NarrationVoice } from '../audioGuide'

/** Response of `POST /texttospeech/generate` (BE `SynthesizeAudioDto`). */
export interface GeneratedNarration {
  audioUrl: string
  assetKey: string
  contentType: string
  byteLength: number
  durationSeconds: number
  voiceName: string
}

/** Turns a Vietnamese script into an MP3 with Azure TTS; the file is kept in private Blob storage. */
export function generateNarration(text: string, voiceName: NarrationVoice): Promise<GeneratedNarration> {
  return apiRequest<GeneratedNarration>(API_ENDPOINTS.textToSpeech.generate, {
    method: 'POST',
    body: { text, voiceName },
  })
}

/** The MP3 of a generated narration, read through the backend because the Blob container is private. */
export function fetchNarrationAudio(assetKey: string): Promise<Blob> {
  return apiRequest<Blob>(API_ENDPOINTS.textToSpeech.audio, { query: { assetKey }, responseType: 'blob' })
}
