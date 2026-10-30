import { describe, expect, it } from 'vitest'
import type { AdminTripStatus } from './types'
import { DISPATCH_OPERATIONS, isFinished, isUpcomingOrRunning } from './utils'

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

describe('trip groups', () => {
  it('splits statuses into upcoming/running, arrived and finished', () => {
    expect(STATUSES.filter(isUpcomingOrRunning)).toEqual(['Scheduled', 'Boarding', 'EnRoute', 'Suspended'])
    expect(STATUSES.filter(isFinished)).toEqual(['Completed', 'Cancelled', 'Terminated'])
    expect(isFinished('Arrived')).toBe(false)
    expect(isUpcomingOrRunning('Arrived')).toBe(false)
  })

  it('the admin can suspend, resume, cancel, terminate and close', () => {
    expect(DISPATCH_OPERATIONS).toEqual(['Suspend', 'Resume', 'Cancel', 'Terminate', 'Complete'])
  })
})
