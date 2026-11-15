import { describe, expect, it } from 'vitest'
import { stopRowsError } from './scheduleStops'
import type { ScheduleStopRow } from './scheduleStops'

const row = (id: string, arrival: string, departure: string, included = true): ScheduleStopRow => ({
  routeStopId: id,
  stationName: id,
  included,
  arrival,
  departure,
})

describe('stopRowsError', () => {
  const ok = [row('a', '', ''), row('b', '10', '12'), row('c', '25', '')]

  it('accepts times that only move forward', () => {
    expect(stopRowsError(ok)).toBeUndefined()
  })

  it('needs two served stops', () => {
    expect(stopRowsError([row('a', '', ''), row('b', '10', '', false)])).toBeTruthy()
  })

  it.each(['1e1', ' ', '-5', '1.5', 'abc', '1441'])('refuses %j as minutes', (bad) => {
    expect(stopRowsError([row('a', '', ''), row('b', bad, '12'), row('c', '25', '')])).toBeTruthy()
  })

  it('refuses arriving before the previous stop is left, leaving before arriving, and a last stop at minute 0', () => {
    expect(stopRowsError([row('a', '', ''), row('b', '10', '12'), row('c', '11', '')])).toBeTruthy()
    expect(stopRowsError([row('a', '', ''), row('b', '10', '8'), row('c', '25', '')])).toBeTruthy()
    expect(stopRowsError([row('a', '', ''), row('b', '0', '')])).toBeTruthy()
  })
})
