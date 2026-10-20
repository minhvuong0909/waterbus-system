import { describe, expect, it } from 'vitest'
import reducer, { holdStarted, orderReleased, orderSettled, toggleSeat } from './bookingSlice'
import type { BookingState } from './bookingSlice'

const hold = {
  orderId: 'order-1', bookingId: 'booking-1', seatIds: ['s1', 's2'], submitted: false,
  accessToken: 'guest', bookingCodes: ['WB1'], totalAmount: 40000, expiresAt: 1_000_000,
}
const start = () => reducer(undefined, holdStarted(hold)) as BookingState

describe('booking order ownership', () => {
  it('remembers which seats the order holds, even after the selection changes', () => {
    const state = reducer(start(), toggleSeat('s3'))
    expect(state.hold).toBeNull()
    expect(state.order).toMatchObject({ orderId: 'order-1', seatIds: ['s1', 's2'], heldUntil: 1_000_000 })
  })

  it('forgets the order once it was cancelled to hold other seats', () => {
    expect(reducer(start(), orderReleased()).order).toBeNull()
  })

  it('keeps a settled order so its result can still be read', () => {
    const settled = reducer(start(), orderSettled())
    expect(reducer(settled, orderReleased()).order).toMatchObject({ orderId: 'order-1', settled: true })
  })
})
