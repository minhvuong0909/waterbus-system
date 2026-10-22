import { describe, expect, it } from 'vitest'
import { formatVnd, formatVndSuffix } from './format'
import { toVietnamDate } from './time'

describe('format', () => {
  it('writes money the Vietnamese way', () => {
    expect(formatVnd(15000)).toBe('15.000 VND')
    expect(formatVndSuffix(1250000)).toBe('1.250.000 VND')
  })

  it('keeps zero and small amounts readable', () => {
    expect(formatVnd(0)).toBe('0 VND')
  })
})

describe('toVietnamDate', () => {
  it('uses the Vietnam calendar day, not the UTC one', () => {
    // 20:00 UTC on 1 Oct is 03:00 on 2 Oct in Vietnam (UTC+7).
    expect(toVietnamDate(new Date('2026-10-01T20:00:00Z'))).toBe('2026-10-02')
  })

  it('stays on the same day before the midnight boundary', () => {
    expect(toVietnamDate(new Date('2026-10-01T16:59:00Z'))).toBe('2026-10-01')
  })
})
