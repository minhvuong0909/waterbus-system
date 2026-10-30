import { describe, expect, it } from 'vitest'
import { alertMetrics, alertNote, decisionOperation, pendingCountOf, sortAlerts, timeAgo } from './alertUtils'
import type { WeatherAlert } from './types'

const REAL =
  'Cảnh báo thời tiết tuyến RT01: trời quang, gió 43,2 km/h, gió giật 68,4 km/h. ' +
  'Admin xem xét từng chuyến bị ảnh hưởng; hệ thống không tự đổi trạng thái chuyến.'

const alert = (patch: Partial<WeatherAlert>): WeatherAlert => ({
  id: 'a', level: 1, message: '', issuedAt: '2026-10-20T15:00:00Z', kind: 'Weather', ...patch,
})

describe('alertMetrics', () => {
  it('reads the sky, wind and gusts the weather guard writes, with a decimal comma', () => {
    expect(alertMetrics({ message: REAL })).toEqual({ condition: 'trời quang', windKmh: 43.2, gustKmh: 68.4 })
  })

  it('works without gusts', () => {
    expect(alertMetrics({ message: 'Cảnh báo thời tiết tuyến RT01: mưa nhẹ, gió 38 km/h. Admin xem xét.' }))
      .toEqual({ condition: 'mưa nhẹ', windKmh: 38, gustKmh: undefined })
  })

  it('gives nothing for a free text report', () => {
    expect(alertMetrics({ message: 'Engine fault on SWB-01.' })).toEqual({})
  })
})

describe('alertNote', () => {
  it('keeps only the advice after the measurements', () => {
    expect(alertNote(REAL)).toBe('Admin xem xét từng chuyến bị ảnh hưởng; hệ thống không tự đổi trạng thái chuyến.')
  })

  it('keeps a message it cannot read as it is', () => {
    expect(alertNote('Captain reported an engine fault.')).toBe('Captain reported an engine fault.')
  })
})

describe('pendingCountOf', () => {
  it('counts undecided trips when they are loaded, else uses the backend count', () => {
    expect(pendingCountOf({ trips: [{ tripId: '1', decision: null }, { tripId: '2', decision: 'Suspend' }] })).toBe(1)
    expect(pendingCountOf({ pendingDecisionCount: 3 })).toBe(3)
    expect(pendingCountOf({})).toBe(0)
  })
})

describe('sortAlerts', () => {
  it('puts serious alerts and captain reports first, then the newest', () => {
    const sorted = sortAlerts([
      alert({ id: 'old-warning', issuedAt: '2026-10-20T10:00:00Z' }),
      alert({ id: 'new-warning', issuedAt: '2026-10-20T12:00:00Z' }),
      alert({ id: 'high', level: 2, issuedAt: '2026-10-20T09:00:00Z' }),
      alert({ id: 'sos', kind: 'Technical', issuedAt: '2026-10-20T11:00:00Z' }),
    ])
    expect(sorted.map((a) => a.id)).toEqual(['sos', 'high', 'new-warning', 'old-warning'])
  })
})

describe('timeAgo', () => {
  const now = Date.parse('2026-10-20T15:30:00Z')
  it('speaks in minutes, then hours', () => {
    expect(timeAgo('2026-10-20T15:29:40Z', now)).toBe('just now')
    expect(timeAgo('2026-10-20T15:18:00Z', now)).toBe('12 min ago')
    expect(timeAgo('2026-10-20T12:30:00Z', now)).toBe('3 h ago')
  })
})

describe('decisionOperation', () => {
  it('reads the backend "None" as no action needed', () => {
    expect(decisionOperation('None')).toBe('NoAction')
  })

  it('keeps the operation names, and gives null for an unknown value', () => {
    expect(decisionOperation('Suspend')).toBe('Suspend')
    expect(decisionOperation('Cancel')).toBe('Cancel')
    expect(decisionOperation('Whatever')).toBeNull()
  })
})
