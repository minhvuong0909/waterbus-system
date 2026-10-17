import { describe, expect, it } from 'vitest'
import type { AdminTrip, AdminTripStatus } from './types'
import {
  DISPATCH_OPERATIONS,
  OPERATION_RESULT,
  allowedOperations,
  allowedOperationsOf,
  isFinished,
  isUpcomingOrRunning,
  tripBoatCode,
  tripRouteName,
} from './utils'

const STATUSES: AdminTripStatus[] = [
  'Scheduled',
  'Boarding',
  'EnRoute',
  'Suspended',
  'Arrived',
  'Completed',
  'Cancelled',
  'Terminated',
]

const trip = (patch: Partial<AdminTrip> = {}): AdminTrip => ({
  id: 't1',
  scheduleId: null,
  routeId: 'r1',
  boatId: 'b1',
  departureTime: '2026-10-17T08:00:00Z',
  arrivalTime: '2026-10-17T08:45:00Z',
  tripType: 'Commuter',
  status: 'Scheduled',
  basePrice: 0,
  ...patch,
})

describe('trip lifecycle rules (mock)', () => {
  it('every allowed operation leads to a status the lifecycle knows', () => {
    STATUSES.forEach((status) =>
      allowedOperations(status).forEach((op) => expect(OPERATION_RESULT[op]).not.toBeNull()),
    )
  })

  it('a finished trip allows nothing', () => {
    const finished: AdminTripStatus[] = ['Completed', 'Cancelled', 'Terminated']
    finished.forEach((s) => expect(allowedOperations(s)).toEqual([]))
  })

  it('only a running trip can be terminated, and only a trip that has not run can be cancelled', () => {
    expect(allowedOperations('EnRoute')).toContain('Terminate')
    expect(allowedOperations('EnRoute')).not.toContain('Cancel')
    expect(allowedOperations('Scheduled')).toContain('Cancel')
    expect(allowedOperations('Scheduled')).not.toContain('Terminate')
  })

  it('an arrived trip can only be closed', () => {
    expect(allowedOperations('Arrived')).toEqual(['Complete'])
  })

  it('records decisions without changing the trip', () => {
    expect(OPERATION_RESULT.NoAction).toBeNull()
    expect(OPERATION_RESULT.SOS).toBeNull()
  })
})

describe('allowedOperationsOf', () => {
  it("prefers what the backend reports over the mock rules", () => {
    expect(allowedOperationsOf(trip({ status: 'Suspended', allowedOperations: ['Terminate'] }))).toEqual(['Terminate'])
  })

  it('falls back to the mock rules when the trip does not carry them', () => {
    expect(allowedOperationsOf(trip({ status: 'EnRoute' }))).toEqual(allowedOperations('EnRoute'))
  })

  it('an empty list from the backend means nothing is allowed', () => {
    expect(allowedOperationsOf(trip({ status: 'Scheduled', allowedOperations: [] }))).toEqual([])
  })
})

describe('trip groups', () => {
  it('splits statuses into upcoming/running, arrived and finished', () => {
    expect(STATUSES.filter(isUpcomingOrRunning)).toEqual(['Scheduled', 'Boarding', 'EnRoute', 'Suspended'])
    expect(STATUSES.filter(isFinished)).toEqual(['Completed', 'Cancelled', 'Terminated'])
    expect(isFinished('Arrived')).toBe(false)
    expect(isUpcomingOrRunning('Arrived')).toBe(false)
  })

  it('the dispatcher can suspend, resume, cancel, terminate and close', () => {
    expect(DISPATCH_OPERATIONS).toEqual(['Suspend', 'Resume', 'Cancel', 'Terminate', 'Complete'])
  })
})

describe('trip names', () => {
  it("uses the backend's names and falls back to the lookup", () => {
    expect(tripRouteName(trip({ routeName: 'Bạch Đằng - Linh Đông' }), () => 'x')).toBe('Bạch Đằng - Linh Đông')
    expect(tripRouteName(trip(), (id) => `route ${id}`)).toBe('route r1')
    expect(tripBoatCode(trip({ boatCode: 'SWB-01' }), () => 'x')).toBe('SWB-01')
    expect(tripBoatCode(trip(), (id) => `boat ${id}`)).toBe('boat b1')
  })
})
