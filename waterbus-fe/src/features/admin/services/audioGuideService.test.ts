import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { fetchNarrationAudio, generateNarration } from './audioGuideService'

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
})
