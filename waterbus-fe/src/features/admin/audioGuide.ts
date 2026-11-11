/** A landmark of the database (`GET /audioguides/pois`, BE `AudioGuidePoiResponse`). */
export interface NarrationPoi {
  poiId: string
  name: string
  hasActiveRoutePoi: boolean
  audioGuideId: string | null
  audioAssetUri: string | null
  isPublished: boolean
}

/** Azure neural voices for Vietnamese (`AzureSpeech` voice names). */
export const NARRATION_VOICES = [
  { id: 'vi-VN-HoaiMyNeural', label: 'Female voice (Hoài My)' },
  { id: 'vi-VN-NamMinhNeural', label: 'Male voice (Nam Minh)' },
] as const

export type NarrationVoice = (typeof NARRATION_VOICES)[number]['id']

/** Azure TTS accepts up to 10,000 characters per script (`SynthesizeAudioCommandValidator`). */
export const MAX_NARRATION_LENGTH = 10_000
