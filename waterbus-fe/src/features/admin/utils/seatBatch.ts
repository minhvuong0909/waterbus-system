import type { SeatInput } from '../types'

export interface SeatBatch {
  seatClassId: string
  /** Letters in front of the seat number, e.g. "S" for S01, S02… */
  prefix: string
  firstNumber: number
  count: number
  firstRow: number
  seatsPerRow: number
}

/** "S" + 7 → "S07": seat codes keep two digits, like the seats already in the fleet. */
export const seatCode = (prefix: string, number: number) =>
  `${prefix}${String(number).padStart(2, '0')}`

/**
 * The seats of a batch, numbered in sequence and laid out row by row from `firstRow`, each row
 * holding `seatsPerRow` seats in columns 1…n.
 */
export function buildSeatBatch(batch: SeatBatch): SeatInput[] {
  return Array.from({ length: batch.count }, (_, i) => ({
    seatCode: seatCode(batch.prefix, batch.firstNumber + i),
    seatClassId: batch.seatClassId,
    rowNumber: batch.firstRow + Math.floor(i / batch.seatsPerRow),
    columnNumber: (i % batch.seatsPerRow) + 1,
  }))
}

/** One cabin of a boat: `count` seats of one class, coded with one prefix. */
export interface SeatBlock {
  seatClassId: string
  prefix: string
  count: number
}

/** What the layout needs to know about the seats a boat already has. */
export interface ExistingSeat {
  seatCode: string
  rowNumber: number
}

/** The planned seats of one block, for the "Adds …" summary. */
export interface PlannedBlock {
  prefix: string
  first: string
  last: string
  count: number
}

/** Backend (SeatInventoryValidators): RowNumber and ColumnNumber of every seat must be within 1…100. */
export const MAX_SEAT_ROW = 100
export const MAX_SEAT_COLUMN = 100

/** The highest row/column a layout needs, or undefined when every seat fits the backend limits. */
export function findLayoutOverflow(seats: readonly SeatInput[]): { row: number } | undefined {
  const row = seats.reduce((max, seat) => Math.max(max, seat.rowNumber), 0)
  const column = seats.reduce((max, seat) => Math.max(max, seat.columnNumber), 0)
  return row > MAX_SEAT_ROW || column > MAX_SEAT_COLUMN ? { row } : undefined
}

export interface SeatLayoutPlan {
  seats: SeatInput[]
  blocks: PlannedBlock[]
}

/**
 * Lays out several blocks of seats in one go. Numbering continues after the seats the boat already
 * has with the same prefix (also across two blocks with one prefix), and each block starts on a fresh
 * row below everything placed so far, so a cabin never shares a row with another.
 */
export function planSeatLayout(
  blocks: SeatBlock[],
  existing: readonly ExistingSeat[],
  seatsPerRow: number,
): SeatLayoutPlan {
  const next = new Map<string, number>()
  let row = 1
  existing.forEach((seat) => {
    row = Math.max(row, seat.rowNumber + 1)
    const match = /^([A-Za-z]+)(\d+)$/.exec(seat.seatCode)
    if (match) {
      const prefix = match[1].toUpperCase()
      next.set(prefix, Math.max(next.get(prefix) ?? 1, Number(match[2]) + 1))
    }
  })

  const seats: SeatInput[] = []
  const planned: PlannedBlock[] = []
  blocks.forEach((block) => {
    const prefix = block.prefix.trim().toUpperCase()
    const firstNumber = next.get(prefix) ?? 1
    seats.push(
      ...buildSeatBatch({
        seatClassId: block.seatClassId,
        prefix,
        firstNumber,
        count: block.count,
        firstRow: row,
        seatsPerRow,
      }),
    )
    planned.push({
      prefix,
      first: seatCode(prefix, firstNumber),
      last: seatCode(prefix, firstNumber + block.count - 1),
      count: block.count,
    })
    next.set(prefix, firstNumber + block.count)
    row += Math.ceil(block.count / seatsPerRow)
  })
  return { seats, blocks: planned }
}
