import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { SeatEvent, SeatMap } from '../types'
import { useLiveSeatMap } from './useLiveSeatMap'

const subscribe = vi.hoisted(() => vi.fn())
vi.mock('../services/seatEvents', () => ({ subscribeSeatEvents: subscribe }))

const TRIP_ID = '11111111-1111-4111-8111-111111111111'
const SOLD_SEAT = '22222222-2222-4222-8222-222222222222'

/** Sơ đồ kín chỗ: không còn ghế `available` nào, hành trình chặng 1→3. */
const fullMap: SeatMap = {
  vesselName: 'SWB-01',
  journey: { boardingCallId: 'a', disembarkingCallId: 'b', boardingVisitOrder: 1, disembarkingVisitOrder: 3 },
  seats: [
    { id: SOLD_SEAT, code: 'S01', category: 'standard', row: 1, column: 'A', status: 'sold', window: true },
  ],
}

let emit: (event: SeatEvent) => void

beforeEach(() => {
  subscribe.mockReset()
  subscribe.mockImplementation((_tripId, _seatIds, listener) => {
    emit = listener
    return () => {}
  })
})

describe('useLiveSeatMap', () => {
  it('subscribes with every seat id, so a fully booked map still hears releases', () => {
    renderHook(() => useLiveSeatMap(fullMap, TRIP_ID, true))

    expect(subscribe).toHaveBeenCalledTimes(1)
    expect(subscribe.mock.calls[0][1]).toEqual([SOLD_SEAT])
  })

  it('frees a seat when a release overlaps the journey', () => {
    const { result } = renderHook(() => useLiveSeatMap(fullMap, TRIP_ID, true))

    act(() =>
      emit({ type: 'SeatUnlocked', tripId: TRIP_ID, seatId: SOLD_SEAT, boardingVisitOrder: 2, disembarkingVisitOrder: 4 }),
    )

    expect(result.current?.seats[0].status).toBe('available')
  })

  it('ignores a release on a segment that does not overlap the journey', () => {
    const { result } = renderHook(() => useLiveSeatMap(fullMap, TRIP_ID, true))

    // [3, 5) nối tiếp [1, 3) nhưng không chồng lấn.
    act(() =>
      emit({ type: 'SeatUnlocked', tripId: TRIP_ID, seatId: SOLD_SEAT, boardingVisitOrder: 3, disembarkingVisitOrder: 5 }),
    )

    expect(result.current?.seats[0].status).toBe('sold')
  })

  it('does not subscribe when live updates are disabled', () => {
    renderHook(() => useLiveSeatMap(fullMap, TRIP_ID, false))

    expect(subscribe).not.toHaveBeenCalled()
  })
})
