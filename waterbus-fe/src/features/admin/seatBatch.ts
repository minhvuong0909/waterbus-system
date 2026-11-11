import type { SeatInput } from './types'

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
