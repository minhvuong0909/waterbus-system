import type { SeatInput } from '../types'

export interface SeatBatch {
  seatClassId: string
  /** Chữ đứng trước số ghế, vd "S" cho S01, S02… */
  prefix: string
  firstNumber: number
  count: number
  firstRow: number
  seatsPerRow: number
}

/** "S" + 7 → "S07": mã ghế giữ hai chữ số, giống các ghế đã có trong đội tàu. */
export const seatCode = (prefix: string, number: number) =>
  `${prefix}${String(number).padStart(2, '0')}`

/**
 * Các ghế của một loạt, đánh số liên tiếp và xếp theo hàng từ `firstRow`, mỗi hàng `seatsPerRow` ghế ở cột 1…n.
 */
export function buildSeatBatch(batch: SeatBatch): SeatInput[] {
  return Array.from({ length: batch.count }, (_, i) => ({
    seatCode: seatCode(batch.prefix, batch.firstNumber + i),
    seatClassId: batch.seatClassId,
    rowNumber: batch.firstRow + Math.floor(i / batch.seatsPerRow),
    columnNumber: (i % batch.seatsPerRow) + 1,
  }))
}
