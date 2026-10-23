import { EXPLORE_DATA } from '../../mocks/explore'

/** A landmark the boat passes, where its narration is played on board. */
export interface NarrationPoi {
  id: string
  title: string
  description: string
  latitude: number
  longitude: number
}

/**
 * Approximate positions of the Explore landmarks along the Saigon River, in river order from
 * Bach Dang pier. The backend has no POI API yet, so the catalog lives here for the admin screen;
 * names and descriptions come from the Explore content so both pages stay the same.
 */
const POSITIONS: Record<string, [latitude: number, longitude: number]> = {
  'customs-house': [10.7716, 106.7063],
  'thu-thiem': [10.7752, 106.7134],
  'ba-son': [10.7835, 106.7086],
  'landmark-park': [10.7946, 106.7224],
}

export const NARRATION_POIS: NarrationPoi[] = Object.entries(POSITIONS).flatMap(([id, [latitude, longitude]]) => {
  const landmark = EXPLORE_DATA.landmarks.find((l) => l.id === id)
  return landmark ? [{ id, title: landmark.title, description: landmark.description, latitude, longitude }] : []
})

/** Azure neural voices for Vietnamese (`AzureSpeech` voice names). */
export const NARRATION_VOICES = [
  { id: 'vi-VN-HoaiMyNeural', label: 'Female voice (Hoài My)' },
  { id: 'vi-VN-NamMinhNeural', label: 'Male voice (Nam Minh)' },
] as const

export type NarrationVoice = (typeof NARRATION_VOICES)[number]['id']

/** Azure TTS accepts up to 10,000 characters per script (`SynthesizeAudioCommandValidator`). */
export const MAX_NARRATION_LENGTH = 10_000
