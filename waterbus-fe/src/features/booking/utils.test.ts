import { describe, expect, it } from 'vitest'
import type { PurchaserForm, SeatPassengerForm } from './types'
import {
  buildCheckoutRequest,
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

describe('buildCheckoutRequest', () => {
  it('sends one passenger per seat, in seat order, and leaves optional fields out', () => {
    const request = buildCheckoutRequest({
      tripId: 't',
      originId: 'o',
      destinationId: 'd',
      seatIds: ['s2', 's1'],
      purchaser: PURCHASER,
      passengers: {
        s1: { fullName: ' A ', email: 'a@x.vn', phone: '' },
        s2: { fullName: 'B', email: '', phone: '0901234567' },
      },
    })
    expect(request.customerPhone).toBeUndefined()
    expect(request.legs).toHaveLength(1)
    expect(request.legs[0]).toMatchObject({
      tripId: 't',
      seatIds: ['s2', 's1'],
      boardingStationId: 'o',
      disembarkingStationId: 'd',
      passengers: [
        { name: 'B', email: undefined, phone: '0901234567' },
        { name: 'A', email: 'a@x.vn', phone: undefined },
      ],
    })
  })
})
