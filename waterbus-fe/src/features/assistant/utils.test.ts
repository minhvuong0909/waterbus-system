import { describe, expect, it } from 'vitest'
import { cardDeparture, cardPrice, parseCardChoice } from './utils'

describe('parseCardChoice', () => {
  it('reads a letter, a number or an ordinal', () => {
    expect(parseCardChoice('chọn chuyến A', 3)).toBe(0)
    expect(parseCardChoice('Chọn 2', 3)).toBe(1)
    expect(parseCardChoice('chọn chuyến thứ ba.', 3)).toBe(2)
    expect(parseCardChoice('choose trip B', 3)).toBe(1)
  })

  it('takes "this one" only when a single card is shown', () => {
    expect(parseCardChoice('chọn này', 1)).toBe(0)
    expect(parseCardChoice('chọn này', 2)).toBe(-1)
  })

  it('leaves a normal question to the assistant', () => {
    expect(parseCardChoice('Có chuyến nào chiều nay không?', 2)).toBeNull()
  })
})

describe('cardPrice', () => {
  const base = { isPriceVerified: true, isDevelopmentPreview: false }
  it('hides a missing or zero fare', () => {
    expect(cardPrice({ ...base, basePrice: null })).toBeNull()
    expect(cardPrice({ ...base, basePrice: 0 })).toBeNull()
  })

  it('hides an unverified fare outside the development preview', () => {
    expect(cardPrice({ basePrice: 15000, isPriceVerified: false, isDevelopmentPreview: false })).toBeNull()
  })

  it('formats a verified fare the Vietnamese way', () => {
    expect(cardPrice({ ...base, basePrice: 15000 })).toBe('15.000 VND')
  })
})

describe('cardDeparture', () => {
  const now = new Date('2026-10-20T05:00:00Z') // 12:00 on 20/10 in Vietnam

  it('says Today or Tomorrow in Vietnam time', () => {
    expect(cardDeparture('2026-10-20T08:00:00Z', now).date).toMatch(/^Today · /)
    expect(cardDeparture('2026-10-21T08:00:00Z', now)).toMatchObject({ time: '15:00' })
    expect(cardDeparture('2026-10-21T08:00:00Z', now).date).toMatch(/^Tomorrow · /)
  })

  it('uses the Vietnam day even when UTC is still the day before', () => {
    // 18:00 UTC on 20/10 is 01:00 on 21/10 in Vietnam.
    expect(cardDeparture('2026-10-20T18:00:00Z', now)).toMatchObject({ time: '01:00' })
    expect(cardDeparture('2026-10-20T18:00:00Z', now).date).toMatch(/^Tomorrow · /)
  })
})
