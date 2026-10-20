import { describe, expect, it } from 'vitest'
import reducer, {
  holdStarted,
  orderReleased,
  orderSettled,
  selectTrip,
  toggleSeat,
  updateSeatPassenger,
} from './bookingSlice'
import type { BookingState } from './bookingSlice'

const hold = {
  orderId: 'order-1', bookingId: 'booking-1', seatIds: ['s1', 's2'], submitted: false,
  accessToken: 'guest', bookingCodes: ['WB1'], totalAmount: 40000, expiresAt: 1_000_000,
}
/** Trip t1 chosen, seats s1 and s2 picked and held, a name filled in. */
const held = (): BookingState => {
  let state = reducer(undefined, selectTrip('t1'))
  state = reducer(state, toggleSeat('s1'))
  state = reducer(state, toggleSeat('s2'))
  state = reducer(state, holdStarted(hold))
  return reducer(state, updateSeatPassenger({ seatId: 's1', patch: { fullName: 'An' } }))
}

describe('booking order ownership', () => {
  it('remembers which seats the order holds and on which trip, even after the selection changes', () => {
    const state = reducer(held(), toggleSeat('s3'))
    expect(state.hold).toBeNull()
    expect(state.order).toMatchObject({ orderId: 'order-1', seatIds: ['s1', 's2'], heldUntil: 1_000_000, tripId: 't1' })
  })

  it('forgets the order once it was cancelled to hold other seats', () => {
    expect(reducer(held(), orderReleased()).order).toBeNull()
  })

  it('keeps a settled order so its result can still be read', () => {
    const settled = reducer(held(), orderSettled())
    expect(reducer(settled, orderReleased()).order).toMatchObject({ orderId: 'order-1', settled: true })
  })
})

describe('selectTrip', () => {
  it('keeps seats, names and hold when the same trip is picked again', () => {
    const state = reducer(held(), selectTrip('t1'))
    expect(state.seatIds).toEqual(['s1', 's2'])
    expect(state.passengers.s1.fullName).toBe('An')
    expect(state.hold?.orderId).toBe('order-1')
  })

  it('starts a new selection for another trip, keeping the unpaid order so it gets cancelled', () => {
    const state = reducer(held(), selectTrip('t2'))
    expect(state.selectedTripId).toBe('t2')
    expect(state.seatIds).toEqual([])
    expect(state.passengers).toEqual({})
    expect(state.hold).toBeNull()
    expect(state.order?.orderId).toBe('order-1')
  })

  it('starts over on the same trip once the previous order was paid', () => {
    const state = reducer(reducer(held(), orderSettled()), selectTrip('t1'))
    expect(state.seatIds).toEqual([])
    expect(state.order).toBeNull()
  })
})
