import { describe, expect, it } from 'vitest'
import reducer, {
  holdExtended,
  holdStarted,
  orderReleased,
  orderSettled,
  selectTrip,
  setBuyerTravels,
  toggleSeat,
  updatePurchaser,
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

  it('moves the hold deadline to the payment window the backend set', () => {
    const state = reducer(held(), holdExtended(2_000_000))
    expect(state.hold?.expiresAt).toBe(2_000_000)
    expect(state.order?.heldUntil).toBe(2_000_000)
  })

  it('does not move the deadline of an order that is already settled', () => {
    const state = reducer(reducer(held(), orderSettled()), holdExtended(2_000_000))
    expect(state.order?.heldUntil).toBe(1_000_000)
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

describe('buyer is also a passenger', () => {
  const buyer = { fullName: 'Lan', email: 'lan@example.com', phone: '0901234567' }
  const picked = (): BookingState => {
    let state = reducer(undefined, selectTrip('t9'))
    state = reducer(state, toggleSeat('s1'))
    state = reducer(state, toggleSeat('s2'))
    return reducer(state, updatePurchaser(buyer))
  }

  it('fills the first seat with the buyer by default and follows later edits', () => {
    let state = picked()
    expect(state.buyerTravels).toBe(true)
    expect(state.passengers.s1).toEqual(buyer)
    state = reducer(state, updatePurchaser({ fullName: 'Lan Nguyen' }))
    expect(state.passengers.s1.fullName).toBe('Lan Nguyen')
    expect(state.passengers.s2).toBeUndefined()
  })

  it('clears the first seat when the buyer books for someone else', () => {
    let state = reducer(picked(), setBuyerTravels(false))
    expect(state.passengers.s1).toEqual({ fullName: '', email: '', phone: '' })
    state = reducer(state, updatePurchaser({ fullName: 'Lan Nguyen' }))
    expect(state.passengers.s1.fullName).toBe('')
  })

  it('moves the buyer to the next seat when the first seat is removed', () => {
    const state = reducer(picked(), toggleSeat('s1'))
    expect(state.seatIds).toEqual(['s2'])
    expect(state.passengers.s2).toEqual(buyer)
    expect(state.passengers.s1).toBeUndefined()
  })
})
