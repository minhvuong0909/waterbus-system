/** Một địa danh trong database (`GET /audioguides/pois`, BE `AudioGuidePoiResponse`). */
export interface NarrationPoi {
  poiId: string
  name: string
  hasActiveRoutePoi: boolean
  audioGuideId: string | null
  audioAssetUri: string | null
  isPublished: boolean
}

/** Các giọng neural tiếng Việt của Azure (tên giọng `AzureSpeech`). */
export const NARRATION_VOICES = [
  { id: 'vi-VN-HoaiMyNeural', label: 'Female voice (Hoài My)' },
  { id: 'vi-VN-NamMinhNeural', label: 'Male voice (Nam Minh)' },
] as const

export type NarrationVoice = (typeof NARRATION_VOICES)[number]['id']

/** Azure TTS nhận tối đa 10.000 ký tự mỗi kịch bản (`SynthesizeAudioCommandValidator`). */
export const MAX_NARRATION_LENGTH = 10_000
