import { memo, useMemo } from 'react'
import { Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import type { Seat, SeatCategory, SeatMap } from '../types'
import { CABIN_ORDER, cabinLabel, seatTags } from '../utils'
import { t } from '../../../i18n'

interface SeatMapViewProps {
  seatMap: SeatMap
  selectedIds: string[]
  onSelect: (id: string) => void
}

const COLUMNS_LEFT = ['A', 'B'] as const

const COLUMNS_RIGHT = ['C', 'D'] as const

// Two seats, the aisle, two seats. Seats shrink on narrow screens so the four columns always fit the hull.
const GRID =
  'grid grid-cols-[repeat(2,minmax(0,64px))_minmax(20px,32px)_repeat(2,minmax(0,64px))] justify-center gap-x-2'

const SeatButton = memo(function SeatButton({
  seat,
  selected,
  onSelect,
}: {
  seat: Seat
  selected: boolean
  onSelect: (id: string) => void
}) {
  const unavailable = seat.status === 'sold' || seat.status === 'held'
  const tags = seatTags(seat)
  // Sold and held seats look the same (neither can be chosen); the label still says which it is.
  const state =
    seat.status === 'sold' ? t('sold') : seat.status === 'held' ? t('held by another traveler') : ''

  return (
    <button
      type="button"
      // A chosen seat can always be removed, even if someone else took it meanwhile.
      disabled={unavailable && !selected}
      aria-pressed={selected}
      aria-label={[t('Seat {id}', { id: seat.code }), tags, state].filter(Boolean).join(', ')}
      onClick={() => onSelect(seat.id)}
      className={`flex h-11 w-full items-center justify-center rounded-lg border text-xs font-semibold transition-colors ${
        selected
          ? 'border-teal-flow bg-teal-flow text-white shadow-sm'
          : unavailable
            ? 'cursor-not-allowed border-transparent bg-surface-container text-outline line-through'
            : 'border-outline-variant bg-white text-deep-river hover:border-teal-flow hover:bg-sand-light'
      }`}
    >
      {seat.code}
    </button>
  )
})

function CabinHeading({
  category,
  priceVnd,
}: {
  category: SeatCategory
  /** Fare of a seat in this cabin. */
  priceVnd?: number
}) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b border-surface-container pb-1.5">
      <h3 className="text-sm font-semibold text-deep-river">{cabinLabel(category)}</h3>
      {priceVnd !== undefined && (
        <span className="text-xs font-semibold text-teal-flow">{formatVndSuffix(priceVnd)}</span>
      )}
    </div>
  )
}

/**
 * Top-down plan of the boat, bow at the top: one block per cabin with its fare, seats A-B, the
 * aisle, then C-D. The outer columns are the window seats, which the caption above them says once
 * instead of a badge on every seat.
 */
export default function SeatMapView({ seatMap, selectedIds, onSelect }: SeatMapViewProps) {
  const cabins = useMemo(
    () =>
      CABIN_ORDER.map((category) => {
        const seats = seatMap.seats.filter((s) => s.category === category)
        const rows = [...new Set(seats.map((s) => s.row))].sort((a, b) => a - b)
        return { category, seats, rows, priceVnd: seats[0]?.priceVnd }
      }).filter((cabin) => cabin.seats.length > 0),
    [seatMap.seats],
  )

  return (
    <div className="rounded-2xl bg-mist/60 p-space-sm sm:p-space-lg">
      <div className="mx-auto max-w-sm rounded-b-3xl rounded-t-[72px] border border-outline-variant/50 bg-white px-space-md pb-space-lg pt-space-lg">
        <p className="flex items-center justify-center gap-1 text-2xs font-semibold uppercase tracking-wider text-on-surface-variant">
          <Icon name="navigation" className="text-sm" />
          {t('Bow')}
        </p>
        <div className={`${GRID} mt-space-md text-center text-2xs text-on-surface-variant`}>
          <span>{t('Window')}</span>
          <span className="col-start-5">{t('Window')}</span>
        </div>

        <div className="mt-2 space-y-space-md">
          {cabins.map(({ category, seats, rows, priceVnd }) => (
            <section key={category} className="space-y-2">
              <CabinHeading category={category} priceVnd={priceVnd} />
              {rows.map((row) => (
                <div key={row} className={GRID}>
                  {[...COLUMNS_LEFT, null, ...COLUMNS_RIGHT].map((column) => {
                    const seat = column && seats.find((s) => s.row === row && s.column === column)
                    return seat ? (
                      <SeatButton
                        key={seat.id}
                        seat={seat}
                        selected={selectedIds.includes(seat.id)}
                        onSelect={onSelect}
                      />
                    ) : (
                      <span key={column ?? 'aisle'} />
                    )
                  })}
                </div>
              ))}
            </section>
          ))}
        </div>
      </div>
    </div>
  )
}
