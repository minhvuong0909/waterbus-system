import { describe, expect, it } from 'vitest'
import { minutesOfClock, toVietnamDate, vietnamMinutesOfDay } from './time'

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
