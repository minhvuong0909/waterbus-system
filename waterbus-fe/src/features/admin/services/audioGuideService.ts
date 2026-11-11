import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { NarrationPoi, NarrationVoice } from '../audioGuide'

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

interface PoisPage {
  items: NarrationPoi[]
  page: number
  pageSize: number
  totalCount: number
}

const POIS_PAGE_SIZE = 50

/** `GET /audioguides/pois`: every POI, reading all pages. */
export async function getNarrationPois(): Promise<NarrationPoi[]> {
  const pois: NarrationPoi[] = []
  for (let page = 1; ; page++) {
    const result = await apiRequest<PoisPage>(API_ENDPOINTS.audioGuides.pois, {
      query: { page, pageSize: POIS_PAGE_SIZE },
    })
    pois.push(...result.items)
    if (!result.items.length || pois.length >= result.totalCount) return pois
  }
}

/** Response of `POST /audioguides/publish` (BE `PublishedAudioGuideResponse`). */
export interface PublishedNarration {
  audioGuideId: string
  poiId: string
  languageCode: string
  audioAssetUri: string
  durationSeconds: number | null
  isPublished: boolean
}

/** Links a generated narration (its Blob `audioUrl`) to a POI and publishes it. */
export function publishNarration(poiId: string, narration: GeneratedNarration): Promise<PublishedNarration> {
  return apiRequest<PublishedNarration>(API_ENDPOINTS.audioGuides.publish, {
    method: 'POST',
    body: { poiId, audioAssetUri: narration.audioUrl, durationSeconds: narration.durationSeconds },
  })
}
