import { describe, expect, it } from 'vitest'
import { daysLabel, formatDays, parseDays } from './scheduleDays'
import { buildSeatBatch } from './seatBatch'

describe('seat batch', () => {
  it('numbers the seats in sequence and fills the rows left to right', () => {
    const seats = buildSeatBatch({ seatClassId: 'c1', prefix: 'S', firstNumber: 9, count: 5, firstRow: 3, seatsPerRow: 4 })
    expect(seats.map((s) => s.seatCode)).toEqual(['S09', 'S10', 'S11', 'S12', 'S13'])
    expect(seats.map((s) => [s.rowNumber, s.columnNumber])).toEqual([[3, 1], [3, 2], [3, 3], [3, 4], [4, 1]])
    expect(seats.every((s) => s.seatClassId === 'c1')).toBe(true)
  })

  it('keeps codes above 99 whole', () => {
    expect(buildSeatBatch({ seatClassId: 'c1', prefix: 'S', firstNumber: 100, count: 1, firstRow: 1, seatsPerRow: 4 })[0].seatCode).toBe('S100')
  })
})

describe('schedule days', () => {
  it('reads and writes the ISO weekday list', () => {
    expect(parseDays('5,1,2')).toEqual([1, 2, 5])
    expect(parseDays('1,x,9,')).toEqual([1])
    expect(formatDays([7, 1, 7, 3])).toBe('1,3,7')
  })

  it('describes the operating days', () => {
    expect(daysLabel('1,2,3,4,5,6,7')).toBe('Every day')
    expect(daysLabel('1,2,3,4,5')).toBe('Mon – Fri')
    expect(daysLabel('6,7')).toBe('Sat, Sun')
    expect(daysLabel('1,3,5')).toBe('Mon, Wed, Fri')
  })
})
