import { describe, expect, it } from 'vitest'
import { alertTone, commonDecisions, decisionOperation, isCaptainReport, isStaffReport, pendingCountOf, reporterOf, sortAlerts, timeAgo, titleOf } from './alertUtils'
import type { IncidentAlert, OperationType } from '../../operations/types'

const alert = (patch: Partial<IncidentAlert>): IncidentAlert => ({
  id: 'a', level: 1, message: '', issuedAt: '2026-10-20T15:00:00Z', kind: 'Technical', source: 'Staff', ...patch,
})

describe('captain reports', () => {
  it('are recognised by their source, not by guessing from the incident type', () => {
    expect(isCaptainReport({ source: 'Captain' })).toBe(true)
    expect(isCaptainReport({ source: 'Staff' })).toBe(false)
    expect(isCaptainReport({})).toBe(false)
    // a Safety incident from someone else is not a captain report
    expect(isCaptainReport(alert({ kind: 'Safety', source: 'System' }))).toBe(false)
  })

  it('are titled and toned as an emergency, with their own description kept', () => {
    const sos = alert({ kind: 'Safety', source: 'Captain', level: 2, message: 'Cháy động cơ' })
    expect(titleOf(sos)).toBe('Captain emergency report')
    expect(alertTone(sos)).toMatchObject({ label: 'Captain report', icon: 'sos' })
  })
})

describe('incident source labels (BR-INC-04)', () => {
  it('tells a staff report apart from a system or operations incident', () => {
    expect(isStaffReport({ source: 'Staff' })).toBe(true)
    expect(titleOf(alert({ source: 'Staff' }))).toBe('Staff incident report')
    expect(reporterOf({ source: 'Staff' })).toBe('Reported by staff')
    expect(titleOf(alert({ source: 'System' }))).toBe('Operational incident')
    expect(reporterOf({ source: 'System' })).toBe('Reported by operations')
    expect(reporterOf({})).toBe('Reported by operations')
    expect(reporterOf({ source: 'Captain' })).toBe('Reported by the captain')
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
      alert({ id: 'sos', kind: 'Safety', source: 'Captain', issuedAt: '2026-10-20T11:00:00Z' }),
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

describe('commonDecisions', () => {
  it('offers only what every selected trip allows, plus no action', () => {
    const scheduled = { allowedOperations: ['Suspend', 'Cancel'] as OperationType[] }
    const sailing = { allowedOperations: ['Suspend', 'Terminate'] as OperationType[] }
    expect(commonDecisions([scheduled, sailing])).toEqual(['Suspend', 'NoAction'])
  })

  it('never offers routine operations such as resume', () => {
    expect(commonDecisions([{ allowedOperations: ['Resume', 'Cancel'] }])).toEqual(['Cancel', 'NoAction'])
  })

  it('offers nothing when no trip is selected', () => {
    expect(commonDecisions([])).toEqual([])
  })
})
