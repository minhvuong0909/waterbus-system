import { describe, expect, it } from 'vitest'
import {
  MAX_NARRATION_CHARACTERS,
  POI_SUGGESTIONS,
  getSuggestionForPoi,
} from './audioGuide'

describe('getSuggestionForPoi', () => {
  it('fits every suggested script inside the narration budget', () => {
    for (const script of Object.values(POI_SUGGESTIONS)) {
      expect(script.length).toBeLessThanOrEqual(MAX_NARRATION_CHARACTERS)
    }
  })

  it('returns a publishable script for every known landmark', () => {
    const landmarks = [
      'Cột cờ Thủ Ngữ & Bến Nhà Rồng',
      'Cầu Ba Son (Cầu Thủ Thiêm 2)',
      'Cầu Bình Lợi lịch sử',
      'Tòa tháp Landmark 81 & Vinhomes Central Park',
      'Bán đảo Thanh Đa & Bình Quới',
    ]

    for (const landmark of landmarks) {
      const script = getSuggestionForPoi(landmark)
      expect(script.length).toBeLessThanOrEqual(MAX_NARRATION_CHARACTERS)
      expect(script.length).toBeGreaterThan(0)
    }
  })

  it('caps the fallback script for an unknown landmark with a long name', () => {
    const script = getSuggestionForPoi(`Địa danh chưa có tên ${'x'.repeat(200)}`)

    expect(script.length).toBeLessThanOrEqual(MAX_NARRATION_CHARACTERS)
  })
})
