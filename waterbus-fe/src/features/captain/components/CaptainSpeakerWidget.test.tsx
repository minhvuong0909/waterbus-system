import { describe, expect, it } from 'vitest'
import { getAnnouncementSentence } from './CaptainSpeakerWidget'
import type { StopAnnouncementEvent } from '../../operations/hooks/useFleetHub'

describe('getAnnouncementSentence', () => {
  it('formats departing sentence with current and next station', () => {
    const event: StopAnnouncementEvent = {
      tripId: 'trip-1',
      tripType: 'Commuter',
      type: 'departing',
      stationName: 'Bạch Đằng',
      nextStationName: 'Thủ Thiêm',
    }
    const sentence = getAnnouncementSentence(event)
    expect(sentence).toContain('Bạch Đằng')
    expect(sentence).toContain('Thủ Thiêm')
    expect(sentence).toContain('đang rời bến')
  })

  it('formats near station sentence with approaching station', () => {
    const event: StopAnnouncementEvent = {
      tripId: 'trip-1',
      tripType: 'Commuter',
      type: 'near',
      stationName: 'Bình An',
    }
    const sentence = getAnnouncementSentence(event)
    expect(sentence).toContain('Bình An')
    expect(sentence).toContain('sắp đến bến')
  })

  it('formats arrived station sentence', () => {
    const event: StopAnnouncementEvent = {
      tripId: 'trip-1',
      tripType: 'Commuter',
      type: 'arrived',
      stationName: 'Thanh Đa',
    }
    const sentence = getAnnouncementSentence(event)
    expect(sentence).toContain('Thanh Đa')
    expect(sentence).toContain('đã đến bến')
  })

  it('handles missing station names gracefully with fallback', () => {
    const event = {
      tripId: 'trip-1',
      tripType: 'Commuter',
      type: 'departing',
    } as unknown as StopAnnouncementEvent

    const sentence = getAnnouncementSentence(event)
    expect(sentence).toContain('Bến tàu')
  })
})
