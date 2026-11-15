import { Fragment } from 'react'
import { t } from '../../../i18n'
import type { BoatSeat } from '../types'

interface Cabin {
  seatClassId: string
  name: string
  rows: BoatSeat[][]
}

/** Ghế gom theo hạng (mỗi hạng một khoang), rồi theo hàng, theo thứ tự cột. */
function toCabins(seats: BoatSeat[]): Cabin[] {
  const cabins = new Map<string, { name: string; rows: Map<number, BoatSeat[]> }>()
  for (const seat of seats) {
    const cabin = cabins.get(seat.seatClassId) ?? { name: seat.seatClassName, rows: new Map() }
    cabins.set(seat.seatClassId, cabin)
    cabin.rows.set(seat.rowNumber, [...(cabin.rows.get(seat.rowNumber) ?? []), seat])
  }
  return [...cabins].map(([seatClassId, cabin]) => ({
    seatClassId,
    name: cabin.name,
    rows: [...cabin.rows.entries()]
      .sort(([a], [b]) => a - b)
      .map(([, row]) => row.sort((a, b) => a.columnNumber - b.columnNumber)),
  }))
}

/** Sơ đồ ghế của một tàu, mỗi hạng ghế một khối. Bấm vào ghế để sửa. */
export default function BoatSeatPlan({
  seats,
  onOpen,
}: {
  seats: BoatSeat[]
  onOpen: (seat: BoatSeat) => void
}) {
  return (
    <div className="space-y-5">
      {toCabins(seats).map((cabin) => (
        <section key={cabin.seatClassId}>
          <h3 className="mb-2 flex items-baseline gap-2 text-sm font-semibold text-deep-river">
            {cabin.name}
            <span className="text-xs font-normal text-on-surface-variant">
              {(() => {
                const all = cabin.rows.flat()
                const off = all.filter((seat) => !seat.isActive).length
                return off > 0
                  ? t('{count} seats ({off} switched off)', { count: all.length, off })
                  : t('{count} seats', { count: all.length })
              })()}
            </span>
          </h3>
          <div className="space-y-1.5 overflow-x-auto pb-1">
            {cabin.rows.map((row) => (
              <div key={row[0].rowNumber} className="flex gap-1.5">
                <span className="w-6 shrink-0 self-center text-2xs text-on-surface-variant">{row[0].rowNumber}</span>
                {row.map((seat, i) => (
                  <Fragment key={seat.id}>
                    {/* Cột bị bỏ trống giữ chỗ để sơ đồ không dồn sát. */}
                    {Array.from({ length: Math.max(0, seat.columnNumber - (i === 0 ? 1 : row[i - 1].columnNumber + 1)) }, (_, gap) => (
                      <span key={gap} className="w-14 shrink-0" aria-hidden />
                    ))}
                  <button
                    type="button"
                    onClick={() => onOpen(seat)}
                    aria-label={
                      seat.isActive
                        ? t('Seat {code}', { code: seat.seatCode })
                        : t('Seat {code}, inactive', { code: seat.seatCode })
                    }
                    className={`flex h-10 w-14 shrink-0 items-center justify-center rounded-xl border font-mono text-xs font-semibold transition-colors ${
                      seat.isActive
                        ? 'border-outline-variant/70 bg-white text-deep-river hover:border-teal-flow hover:text-teal-flow'
                        : 'border-dashed border-outline-variant bg-surface-container-low text-on-surface-variant/70 line-through hover:border-teal-flow'
                    }`}
                  >
                    {seat.seatCode}
                  </button>
                  </Fragment>
                ))}
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}
