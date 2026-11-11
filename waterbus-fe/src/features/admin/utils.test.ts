import { describe, expect, it } from 'vitest'
import { DISPATCH_OPERATIONS, isBoardingStop, isFinished, isUpcomingOrRunning } from './utils'
import type { TripStatus, TripStopDto } from '../trips/types'

const STATUSES: TripStatus[] = [
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

describe('isBoardingStop', () => {
  const stop = (patch: Partial<TripStopDto>): TripStopDto => ({
    callId: 'c', stationId: 's', stationName: 'S', visitOrder: 1, canBoard: true, canAlight: true, status: 'Planned',
    estimatedDepartureTime: '2026-10-21T01:00:00Z', ...patch,
  })

  it('keeps a stop where passengers board before the boat leaves', () => {
    expect(isBoardingStop(stop({}))).toBe(true)
  })

  it('drops the last stop, which has no departure, and a stop nobody boards', () => {
    expect(isBoardingStop(stop({ estimatedDepartureTime: null }))).toBe(false)
    expect(isBoardingStop(stop({ estimatedDepartureTime: undefined }))).toBe(false)
    expect(isBoardingStop(stop({ canBoard: false }))).toBe(false)
  })
})
