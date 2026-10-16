import { describe, expect, it } from 'vitest'
import type { PurchaserForm, SeatPassengerForm } from './types'
import {
  buildHoldRequest,
  buildSubmitRequest,
  computeTotals,
  pickJourney,
  toSeatMap,
  isBookingComplete,
  purchaserErrors,
  seatPassengerErrors,
  toCustomerPhone,
} from './utils'

const PURCHASER: PurchaserForm = {
  fullName: 'Nguyen Van An',
  phoneCode: '+84',
  phone: '',
  email: 'an@example.com',
}
const PASSENGER: SeatPassengerForm = { fullName: 'Tran Thi B', email: '', phone: '' }

describe('purchaser (BR-COM-07)', () => {
  it('needs a name and an email but not a phone', () => {
    expect(purchaserErrors(PURCHASER)).toEqual({})
  })

  it('reports a missing name, a bad email and a short phone', () => {
    const errors = purchaserErrors({ ...PURCHASER, fullName: ' ', email: 'nope', phone: '12345' })
    expect(Object.keys(errors).sort()).toEqual(['email', 'fullName', 'phone'])
  })

  it('writes the phone as the backend expects, or nothing when empty', () => {
    expect(toCustomerPhone({ phone: '908 123 456' })).toBe('0908123456')
    expect(toCustomerPhone({ phone: '0908123456' })).toBe('0908123456')
    expect(toCustomerPhone({ phone: '  ' })).toBeUndefined()
  })
})

describe('seat passenger (BR-COM-06)', () => {
  it('needs only a name', () => {
    expect(seatPassengerErrors(PASSENGER)).toEqual({})
    expect(seatPassengerErrors(undefined).fullName).toBeTruthy()
    expect(seatPassengerErrors({ ...PASSENGER, fullName: '' }).fullName).toBeTruthy()
  })

  it('rejects an email that is filled in but invalid', () => {
    expect(seatPassengerErrors({ ...PASSENGER, email: 'x' }).email).toBeTruthy()
  })
})

describe('isBookingComplete', () => {
  it('needs every chosen seat to have a passenger', () => {
    const passengers = { s1: PASSENGER }
    expect(isBookingComplete(PURCHASER, ['s1'], passengers)).toBe(true)
    expect(isBookingComplete(PURCHASER, ['s1', 's2'], passengers)).toBe(false)
    expect(isBookingComplete(PURCHASER, [], passengers)).toBe(false)
  })
})

describe('buildHoldRequest', () => {
  it('names the real stops when the journey is known', () => {
    const request = buildHoldRequest({
      tripId: 't',
      originId: 'o',
      destinationId: 'd',
      seatIds: ['s1'],
      journey: {
        boardingCallId: 'cb',
        disembarkingCallId: 'cd',
        boardingVisitOrder: 1,
        disembarkingVisitOrder: 3,
      },
    })
    expect(request.legs[0]).toMatchObject({
      tripId: 't',
      seatIds: ['s1'],
      boardingStationId: 'o',
      disembarkingStationId: 'd',
      boardingCallId: 'cb',
      disembarkingCallId: 'cd',
    })
  })
})

describe('buildSubmitRequest', () => {
  it('sends one passenger per held seat, tied to the booking, and leaves optional fields out', () => {
    const request = buildSubmitRequest({
      bookingId: 'b',
      seatIds: ['s2', 's1'],
      purchaser: PURCHASER,
      passengers: {
        s1: { fullName: ' A ', email: 'a@x.vn', phone: '' },
        s2: { fullName: 'B', email: '', phone: '0901234567' },
      },
    })
    expect(request).toMatchObject({ customerName: 'Nguyen Van An', customerEmail: 'an@example.com' })
    expect(request.customerPhone).toBeUndefined()
    expect(request.passengers).toEqual([
      { bookingId: 'b', seatId: 's2', name: 'B', email: undefined, phone: '0901234567' },
      { bookingId: 'b', seatId: 's1', name: 'A', email: 'a@x.vn', phone: undefined },
    ])
  })
})

const stop = (callId: string, stationId: string, visitOrder: number, extra = {}) => ({
  callId,
  stationId,
  stationName: stationId,
  visitOrder,
  canBoard: true,
  canAlight: true,
  status: 'Scheduled',
  ...extra,
})

describe('pickJourney', () => {
  it('uses the stops of the trip in visit order', () => {
    const stops = [stop('c1', 'A', 1), stop('c2', 'B', 2), stop('c3', 'C', 3)]
    const journey = pickJourney(stops, 'A', 'C')
    expect(journey?.boarding.callId).toBe('c1')
    expect(journey?.disembarking.callId).toBe('c3')
  })

  it('rejects the wrong direction and skipped stops', () => {
    const stops = [stop('c1', 'A', 1), stop('c2', 'B', 2), stop('c3', 'C', 3, { status: 'Skipped' })]
    expect(pickJourney(stops, 'B', 'A')).toBeNull()
    expect(pickJourney(stops, 'A', 'C')).toBeNull()
  })

  it('tells apart two calls at the same pier on a loop trip', () => {
    const stops = [stop('c1', 'A', 1), stop('c2', 'B', 2), stop('c3', 'A', 3)]
    expect(pickJourney(stops, 'B', 'A')?.disembarking.callId).toBe('c3')
  })
})

describe('toSeatMap', () => {
  const seat = (id: string, code: string, col: number, cls: string, price: number | null, status: string) => ({
    seatId: id,
    seatCode: code,
    rowNumber: 1,
    columnNumber: col,
    seatClassId: cls,
    seatClassName: cls,
    price,
    currency: 'VND',
    status,
    unavailableReason: null,
    holdExpiresAt: null,
  })
  const dto = {
    tripId: 't',
    boatId: 'b',
    boardingCallId: 'cb',
    disembarkingCallId: 'cd',
    boardingVisitOrder: 1,
    disembarkingVisitOrder: 3,
    canBook: true,
    salesState: 'open',
    availableSeatsCount: 1,
    asOf: '',
    seats: [
      seat('s1', 'S01', 1, 'Standard', 15000, 'available'),
      seat('s2', 'F01', 2, 'Front Cabin', 18000, 'held'),
      seat('s3', 'S02', 3, 'Standard', null, 'unavailable'),
    ],
  }

  it('maps seats, cabins, columns, fares and the journey', () => {
    const map = toSeatMap(dto, { boat: { boatId: 'b', code: 'SWB-01', name: 'Saigon Waterbus 01' } })
    expect(map.vesselName).toBe('Saigon Waterbus 01')
    expect(map.journey).toMatchObject({ boardingCallId: 'cb', disembarkingVisitOrder: 3 })
    expect(map.seats[0]).toMatchObject({
      id: 's1',
      column: 'A',
      window: true,
      category: 'standard',
      status: 'available',
      priceVnd: 15000,
    })
    expect(map.seats[1]).toMatchObject({ category: 'front', status: 'held', window: false })
  })

  it('shows a seat without a fare as taken, and totals use the seat fares', () => {
    const map = toSeatMap(dto, { boat: null })
    expect(map.seats[2].status).toBe('sold')
    const totals = computeTotals({ fareVnd: 0 } as never, [map.seats[0], map.seats[1]])
    expect(totals).toMatchObject({ fareVnd: 33000, seatFeeVnd: 0, totalVnd: 33000 })
  })
})
