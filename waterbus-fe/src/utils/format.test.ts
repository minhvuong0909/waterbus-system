import { describe, expect, it } from 'vitest'
import { formatVnd, formatVndSuffix } from './format'
import { fromVietnamDateTimeInput, toVietnamDate, toVietnamDateTimeInput } from './time'

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

describe('datetime-local in Vietnam time', () => {
  it('shows a UTC instant as the Vietnam wall clock and reads it back', () => {
    expect(toVietnamDateTimeInput('2026-10-31T23:30:00Z')).toBe('2026-11-01T06:30')
    expect(fromVietnamDateTimeInput('2026-11-01T06:30')).toBe('2026-10-31T23:30:00.000Z')
  })

  it('keeps midnight as 00, not 24', () => {
    expect(toVietnamDateTimeInput('2026-10-31T17:00:00Z')).toBe('2026-11-01T00:00')
  })
})
