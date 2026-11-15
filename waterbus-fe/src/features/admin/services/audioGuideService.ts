import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { NarrationPoi, NarrationVoice } from '../utils/audioGuide'

/** Phản hồi của `POST /texttospeech/generate` (BE `SynthesizeAudioDto`). */
export interface GeneratedNarration {
  audioUrl: string
  assetKey: string
  contentType: string
  byteLength: number
  durationSeconds: number
  voiceName: string
}

/** Chuyển kịch bản tiếng Việt thành MP3 bằng Azure TTS; file lưu trong Blob storage riêng tư. */
export function generateNarration(text: string, voiceName: NarrationVoice): Promise<GeneratedNarration> {
  return apiRequest<GeneratedNarration>(API_ENDPOINTS.textToSpeech.generate, {
    method: 'POST',
    body: { text, voiceName },
  })
}

/** MP3 của một bài thuyết minh đã tạo, đọc qua backend vì Blob container là riêng tư. */
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

/** `GET /audioguides/pois`: mọi POI, đọc hết các trang. */
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

/** Phản hồi của `POST /audioguides/publish` (BE `PublishedAudioGuideResponse`). */
export interface PublishedNarration {
  audioGuideId: string
  poiId: string
  languageCode: string
  audioAssetUri: string
  durationSeconds: number | null
  isPublished: boolean
}

/** Gắn bài thuyết minh đã tạo (`audioUrl` trên Blob) vào một POI và xuất bản. */
export function publishNarration(poiId: string, narration: GeneratedNarration): Promise<PublishedNarration> {
  return apiRequest<PublishedNarration>(API_ENDPOINTS.audioGuides.publish, {
    method: 'POST',
    body: { poiId, audioAssetUri: narration.audioUrl, durationSeconds: narration.durationSeconds },
  })
}
