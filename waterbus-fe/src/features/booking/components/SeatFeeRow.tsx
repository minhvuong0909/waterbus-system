import { formatVnd } from '../../../utils/format'
import SummaryRow from './SummaryRow'

/** Seat fee line: "+VND 9,000" for a VIP seat, or `includedText` when the seat is free to reserve. */
export default function SeatFeeRow({
  label,
  seatFeeVnd,
  includedText,
}: {
  label: string
  seatFeeVnd: number
  includedText: string
}) {
  // With backend prices each seat already costs its class fare: a "0 VND, included" line says nothing.
  if (seatFeeVnd <= 0) return null
  return (
    <SummaryRow
      label={label}
      value={seatFeeVnd > 0 ? `+${formatVnd(seatFeeVnd)}` : includedText}
      accent
    />
  )
}
