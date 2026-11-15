import { describe, expect, it } from 'vitest'
import { MAX_SEAT_ROW, buildSeatBatch, findLayoutOverflow, planSeatLayout, seatCode } from './seatBatch'

const FRONT = 'class-front'
const STD = 'class-standard'
const DECK = 'class-deck'

describe('seatCode', () => {
  it('keeps two digits like the seats already in the fleet', () => {
    expect(seatCode('S', 7)).toBe('S07')
    expect(seatCode('F', 12)).toBe('F12')
    expect(seatCode('S', 105)).toBe('S105')
  })
})

describe('buildSeatBatch', () => {
  it('numbers seats in sequence and fills rows left to right', () => {
    const seats = buildSeatBatch({ seatClassId: STD, prefix: 'S', firstNumber: 1, count: 6, firstRow: 2, seatsPerRow: 4 })

    expect(seats.map((s) => [s.seatCode, s.rowNumber, s.columnNumber])).toEqual([
      ['S01', 2, 1], ['S02', 2, 2], ['S03', 2, 3], ['S04', 2, 4], ['S05', 3, 1], ['S06', 3, 2],
    ])
  })
})

describe('planSeatLayout', () => {
  it('lays out a whole 60-seat boat from three blocks, each cabin starting on a fresh row', () => {
    const { seats, blocks } = planSeatLayout(
      [
        { seatClassId: FRONT, prefix: 'F', count: 12 },
        { seatClassId: STD, prefix: 'S', count: 36 },
        { seatClassId: DECK, prefix: 'O', count: 12 },
      ],
      [],
      4,
    )

    expect(seats).toHaveLength(60)
    expect(new Set(seats.map((s) => s.seatCode)).size).toBe(60)                        // không trùng mã
    expect(blocks.map((b) => `${b.first}-${b.last}`)).toEqual(['F01-F12', 'S01-S36', 'O01-O12'])
    expect(seats[11]).toMatchObject({ seatCode: 'F12', rowNumber: 3, columnNumber: 4 })
    expect(seats[12]).toMatchObject({ seatCode: 'S01', rowNumber: 4, columnNumber: 1 })  // khoang mới bắt đầu hàng mới
    expect(seats[59]).toMatchObject({ seatCode: 'O12', rowNumber: 15, columnNumber: 4 })
    expect(seats.filter((s) => s.seatClassId === STD)).toHaveLength(36)
  })

  it('continues numbering and rows after the seats the boat already has', () => {
    const existing = [
      { seatCode: 'S01', rowNumber: 1 }, { seatCode: 'S02', rowNumber: 1 }, { seatCode: 'S10', rowNumber: 3 },
      { seatCode: 'F05', rowNumber: 2 },
    ]

    const { seats, blocks } = planSeatLayout([{ seatClassId: STD, prefix: 's', count: 3 }], existing, 4)

    expect(blocks[0]).toMatchObject({ first: 'S11', last: 'S13' })                      // sau S10, bất kể hoa/thường
    expect(seats.map((s) => s.rowNumber)).toEqual([4, 4, 4])                            // dưới hàng 3
  })

  it('keeps numbering across two blocks that share a prefix', () => {
    const { blocks } = planSeatLayout(
      [{ seatClassId: STD, prefix: 'S', count: 2 }, { seatClassId: DECK, prefix: 'S', count: 2 }],
      [],
      4,
    )

    expect(blocks.map((b) => `${b.first}-${b.last}`)).toEqual(['S01-S02', 'S03-S04'])
  })

  it('handles a partial last row and a single seat', () => {
    const { seats } = planSeatLayout([{ seatClassId: STD, prefix: 'S', count: 5 }, { seatClassId: FRONT, prefix: 'F', count: 1 }], [], 4)

    expect(seats[4]).toMatchObject({ seatCode: 'S05', rowNumber: 2, columnNumber: 1 })
    expect(seats[5]).toMatchObject({ seatCode: 'F01', rowNumber: 3, columnNumber: 1 })  // dù hàng 2 còn chỗ trống
  })

  it('plans nothing for no blocks', () => {
    expect(planSeatLayout([], [], 4)).toEqual({ seats: [], blocks: [] })
  })
})

describe('findLayoutOverflow', () => {
  const layout = (count: number, seatsPerRow: number, existingRow = 0) =>
    planSeatLayout(
      [{ seatClassId: STD, prefix: 'S', count }],
      existingRow ? [{ seatCode: 'Z01', rowNumber: existingRow }] : [],
      seatsPerRow,
    ).seats

  it('accepts a layout whose last row is exactly the limit (100)', () => {
    expect(layout(100, 1)[99].rowNumber).toBe(MAX_SEAT_ROW)
    expect(findLayoutOverflow(layout(100, 1))).toBeUndefined()
  })

  it('rejects a layout one row past the limit (101) and reports the row it needs', () => {
    expect(findLayoutOverflow(layout(101, 1))).toEqual({ row: 101 })
  })

  it('counts the rows the boat already uses, not only the new seats', () => {
    expect(findLayoutOverflow(layout(2, 1, 99))).toEqual({ row: 101 })          // hàng 100 đã có ghế → ghế mới ở hàng 100, 101
    expect(findLayoutOverflow(layout(1, 1, 99))).toBeUndefined()                // ghế mới ở đúng hàng 100
  })

  it('accepts the same 120 seats once enough of them share a row', () => {
    expect(findLayoutOverflow(layout(120, 1))).toEqual({ row: 120 })
    expect(findLayoutOverflow(layout(120, 2))).toBeUndefined()
  })

  it('rejects a row wider than 100 columns', () => {
    const seats = layout(101, 101)
    expect(seats.every((s) => s.rowNumber === 1)).toBe(true)
    expect(findLayoutOverflow(seats)).toEqual({ row: 1 })
  })
})
