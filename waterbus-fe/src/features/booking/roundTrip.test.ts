import { describe, expect, it } from 'vitest'
import reducer, {
  copyPassengersToReturn,
  holdStarted,
  selectReturnTrip,
  selectTrip,
  setQuery,
  setReturnQuery,
  startReturnLeg,
  toggleReturnSeat,
  toggleSeat,
  updateSeatPassenger,
} from './bookingSlice'
import type { BookingState } from './bookingSlice'
import { leavesAfterArrival } from './legs'
import { addTotals, buildHoldRequest, buildSubmitRequest, isBookingComplete, withOwnHold } from './utils'

const A = 'pier-a'
const B = 'pier-b'
const C = 'pier-c'

/** Round trip A → B on 21/10, back on 22/10, outbound trip t1 with seats s1 and s2 named. */
function planned(): BookingState {
  let state = reducer(undefined, setQuery({ tripType: 'round-trip', originId: A, destinationId: B, dateId: '2026-10-21', returnDateId: '2026-10-22' }))
  state = reducer(state, selectTrip('t1'))
  state = reducer(state, toggleSeat('s1'))
  state = reducer(state, toggleSeat('s2'))
  state = reducer(state, updateSeatPassenger({ seatId: 's1', patch: { fullName: 'An' } }))
  return reducer(state, updateSeatPassenger({ seatId: 's2', patch: { fullName: 'Binh' } }))
}

describe('round trip state', () => {
  it('opens the return leg back the other way on the return day', () => {
    const state = reducer(planned(), startReturnLeg())
    expect(state.returnLeg).toMatchObject({ originId: B, destinationId: A, dateId: '2026-10-22' })
  })

  it('lets the return start at another pier (BR-COM-03) and drops its trip when it does', () => {
    let state = reducer(reducer(planned(), startReturnLeg()), selectReturnTrip('t2'))
    state = reducer(state, toggleReturnSeat('r1'))
    state = reducer(state, setReturnQuery({ originId: C }))
    expect(state.returnLeg).toMatchObject({ originId: C, destinationId: A, tripId: null, seatIds: [] })
  })

  it('never keeps a return day before the outbound day', () => {
    let state = reducer(planned(), setQuery({ dateId: '2026-10-25' }))
    expect(state.query.returnDateId).toBe('2026-10-25')
    state = reducer(reducer(state, startReturnLeg()), setReturnQuery({ dateId: '2026-10-23' }))
    expect(state.returnLeg.dateId).toBe('2026-10-25')
  })

  it('sets the return leg up again when the outbound piers change', () => {
    let state = reducer(reducer(planned(), startReturnLeg()), selectReturnTrip('t2'))
    state = reducer(state, setQuery({ originId: C }))
    expect(state.returnLeg).toMatchObject({ originId: '', tripId: null })
    state = reducer(state, startReturnLeg())
    expect(state.returnLeg).toMatchObject({ originId: B, destinationId: C })
  })

  it('copies the outbound passengers to the return seats in order', () => {
    let state = reducer(reducer(planned(), startReturnLeg()), selectReturnTrip('t2'))
    state = reducer(state, toggleReturnSeat('r1'))
    state = reducer(state, toggleReturnSeat('r2'))
    state = reducer(state, copyPassengersToReturn())
    expect(state.returnLeg.passengers.r1.fullName).toBe('An')
    expect(state.returnLeg.passengers.r2.fullName).toBe('Binh')
  })

  it('drops a running hold when a return seat changes', () => {
    let state = reducer(reducer(planned(), startReturnLeg()), selectReturnTrip('t2'))
    state = reducer(state, holdStarted({
      orderId: 'o1', bookingId: 'b1', seatIds: ['s1', 's2'], submitted: false, accessToken: null,
      bookingCodes: ['WB1', 'WB2'], totalAmount: 0, expiresAt: 1_000,
      bookings: [{ bookingId: 'b1', tripId: 't1', seatIds: ['s1', 's2'] }, { bookingId: 'b2', tripId: 't2', seatIds: ['r1'] }],
    }))
    expect(state.order?.legs).toEqual([{ tripId: 't1', seatIds: ['s1', 's2'] }, { tripId: 't2', seatIds: ['r1'] }])
    expect(reducer(state, toggleReturnSeat('r3')).hold).toBeNull()
  })
})

describe('round trip requests', () => {
  it('holds both legs in one request, outbound first (BR-HOLD-02)', () => {
    const request = buildHoldRequest([
      { tripId: 't1', originId: A, destinationId: B, seatIds: ['s1'] },
      { tripId: 't2', originId: B, destinationId: A, seatIds: ['r1'] },
    ])
    expect(request.legs.map((leg) => [leg.tripId, leg.boardingStationId, leg.disembarkingStationId])).toEqual([
      ['t1', A, B],
      ['t2', B, A],
    ])
  })

  it('names each seat under its own booking', () => {
    const body = buildSubmitRequest({
      purchaser: { fullName: 'An', email: 'an@example.com', phoneCode: '+84', phone: '' },
      bookings: [
        { bookingId: 'b1', seatIds: ['s1'], passengers: { s1: { fullName: 'An', email: '', phone: '' } } },
        { bookingId: 'b2', seatIds: ['r1'], passengers: { r1: { fullName: 'Binh', email: '', phone: '' } } },
      ],
    })
    expect(body.passengers?.map((p) => [p.bookingId, p.seatId, p.name])).toEqual([
      ['b1', 's1', 'An'],
      ['b2', 'r1', 'Binh'],
    ])
  })

  it('needs a passenger on every seat of both legs', () => {
    const buyer = { fullName: 'An', email: 'an@example.com', phoneCode: '+84', phone: '' }
    const named = { s1: { fullName: 'An', email: '', phone: '' } }
    expect(isBookingComplete(buyer, ['s1'], named, { seatIds: ['r1'], passengers: {} })).toBe(false)
    expect(isBookingComplete(buyer, ['s1'], named, { seatIds: [], passengers: {} })).toBe(false)
    expect(isBookingComplete(buyer, ['s1'], named, { seatIds: ['r1'], passengers: { r1: named.s1 } })).toBe(true)
  })

  it('adds the totals of both legs', () => {
    const leg = { seatCount: 2, fareVnd: 40000, seatFeeVnd: 0, totalVnd: 40000 }
    expect(addTotals(leg, { seatCount: 1, fareVnd: 20000, seatFeeVnd: 0, totalVnd: 20000 })).toEqual({
      seatCount: 3, fareVnd: 60000, seatFeeVnd: 0, totalVnd: 60000,
    })
    expect(addTotals(leg)).toBe(leg)
  })
})

describe('round trip rules', () => {
  it('only offers a return trip that leaves after the outbound one arrives', () => {
    const outbound = { dateId: '2026-10-21', arriveTime: '15:45' }
    expect(leavesAfterArrival(outbound, { dateId: '2026-10-21', departTime: '15:30' })).toBe(false)
    expect(leavesAfterArrival(outbound, { dateId: '2026-10-21', departTime: '15:45' })).toBe(false)
    expect(leavesAfterArrival(outbound, { dateId: '2026-10-21', departTime: '17:00' })).toBe(true)
    expect(leavesAfterArrival(outbound, { dateId: '2026-10-22', departTime: '07:00' })).toBe(true)
  })

  it('recognises own seats per trip when both legs use the same boat', () => {
    const seat = (id: string) => ({ id, code: id, category: 'standard' as const, row: 1, column: 'A' as const, priceMultiplier: 1, status: 'held' as const, window: false })
    const map = { vesselName: 'SWB-01', seats: [seat('x'), seat('y')] }
    const order = { heldUntil: 2_000, legs: [{ tripId: 't1', seatIds: ['x'] }, { tripId: 't2', seatIds: ['y'] }] }
    expect(withOwnHold(map, order, 't1', 1_000).seats.map((s) => s.status)).toEqual(['available', 'held'])
    expect(withOwnHold(map, order, 't2', 1_000).seats.map((s) => s.status)).toEqual(['held', 'available'])
  })
})
