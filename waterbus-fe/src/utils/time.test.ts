import { describe, expect, it } from 'vitest'
import { minutesOfClock, toVietnamDate, vietnamDayStartIso, vietnamMinutesOfDay } from './time'

describe('vietnamDayStartIso', () => {
  it('trả 00:00 giờ VN của ngày VN chứa thời điểm (buổi tối)', () => {
    // 16:30 UTC ngày 15 = 23:30 giờ VN ngày 15 -> 00:00 VN ngày 15 = 17:00 UTC ngày 14.
    expect(vietnamDayStartIso(new Date('2026-11-15T16:30:00Z'))).toBe('2026-11-14T17:00:00.000Z')
  })

  it('qua nửa đêm giờ VN thì sang ngày VN kế tiếp dù UTC vẫn là ngày cũ', () => {
    // 17:30 UTC ngày 15 = 00:30 giờ VN ngày 16.
    expect(vietnamDayStartIso(new Date('2026-11-15T17:30:00Z'))).toBe('2026-11-15T17:00:00.000Z')
  })

  it('đúng ngay tại 00:00 giờ VN', () => {
    expect(vietnamDayStartIso(new Date('2026-11-15T17:00:00Z'))).toBe('2026-11-15T17:00:00.000Z')
  })
})

describe('Vietnam clock helpers', () => {
  it('reads an HH:mm clock time as minutes since midnight', () => {
    expect(minutesOfClock('07:30')).toBe(450)
    expect(minutesOfClock('00:00')).toBe(0)
  })

  it('uses the Vietnam clock whatever the device time zone is', () => {
    // 01:00 UTC is 08:00 in Vietnam; a device in UTC would read 01:00 from getHours().
    const instant = new Date('2026-11-03T01:00:00Z')
    expect(vietnamMinutesOfDay(instant)).toBe(8 * 60)
    expect(toVietnamDate(instant)).toBe('2026-11-03')
  })

  it('moves to the next Vietnam day after 17:00 UTC', () => {
    const instant = new Date('2026-11-02T17:30:00Z')
    expect(toVietnamDate(instant)).toBe('2026-11-03')
    expect(vietnamMinutesOfDay(instant)).toBe(30)
  })
})
