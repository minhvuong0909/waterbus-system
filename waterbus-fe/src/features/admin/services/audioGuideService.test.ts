import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { fetchNarrationAudio, generateNarration, getNarrationPois, publishNarration } from './audioGuideService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('audioGuideService', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('sends the script and the chosen voice to the TTS API', async () => {
    const generated = { assetKey: 'audio-assets/tts/20261023/a.mp3', durationSeconds: 4 }
    vi.mocked(apiRequest).mockResolvedValue(generated)

    const result = await generateNarration('Xin chào quý khách.', 'vi-VN-NamMinhNeural')

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.textToSpeech.generate, {
      method: 'POST', body: { text: 'Xin chào quý khách.', voiceName: 'vi-VN-NamMinhNeural' },
    })
    expect(result).toBe(generated)
  })

  it('reads the MP3 as a file through the backend', async () => {
    const mp3 = new Blob(['ID3'], { type: 'audio/mpeg' })
    vi.mocked(apiRequest).mockResolvedValue(mp3)

    await expect(fetchNarrationAudio('audio-assets/tts/20261023/a.mp3')).resolves.toBe(mp3)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.textToSpeech.audio, {
      query: { assetKey: 'audio-assets/tts/20261023/a.mp3' }, responseType: 'blob',
    })
  })

  it('propagates TTS errors so the page can show them', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('Azure TTS unavailable'))

    await expect(generateNarration('Xin chào.', 'vi-VN-HoaiMyNeural')).rejects.toThrow('Azure TTS unavailable')
  })

  it('reads every page of POIs', async () => {
    const poi = (id: string) => ({ poiId: id, name: id, hasActiveRoutePoi: true, audioGuideId: null, audioAssetUri: null, isPublished: false })
    vi.mocked(apiRequest)
      .mockResolvedValueOnce({ items: [poi('a')], page: 1, pageSize: 50, totalCount: 2 })
      .mockResolvedValueOnce({ items: [poi('b')], page: 2, pageSize: 50, totalCount: 2 })

    expect((await getNarrationPois()).map((p) => p.poiId)).toEqual(['a', 'b'])
    expect(apiRequest).toHaveBeenLastCalledWith(API_ENDPOINTS.audioGuides.pois, { query: { page: 2, pageSize: 50 } })
  })

  it('stops on an empty page even if the count is larger', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 5 })
    await expect(getNarrationPois()).resolves.toEqual([])
    expect(apiRequest).toHaveBeenCalledTimes(1)
  })

  it('publishes the generated audio URL for the POI', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ isPublished: true })
    const narration = { audioUrl: 'https://blob/a.mp3', assetKey: 'k', contentType: 'audio/mpeg', byteLength: 1, durationSeconds: 7, voiceName: 'vi-VN-HoaiMyNeural' }

    await publishNarration('poi-1', narration)

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.audioGuides.publish, {
      method: 'POST', body: { poiId: 'poi-1', audioAssetUri: 'https://blob/a.mp3', durationSeconds: 7 },
    })
  })
})
