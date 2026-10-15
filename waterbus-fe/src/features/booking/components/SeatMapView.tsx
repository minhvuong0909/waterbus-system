import { memo, useMemo } from 'react'
import { Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import type { Seat, SeatCategory, SeatMap } from '../types'
import { CABIN_ORDER, cabinLabel, seatFeeFor, seatTags } from '../utils'
import { t } from '../../../i18n'

interface SeatMapViewProps {
  seatMap: SeatMap
  selectedIds: string[]
  /** Fare of the selected trip, used to show each cabin's surcharge. */
  fareVnd: number
  onSelect: (id: string) => void
}

const COLUMNS_LEFT = ['A', 'B'] as const
const COLUMNS_RIGHT = ['C', 'D'] as const
// Seats shrink on narrow screens (up to 62px wide) so the four columns always fit inside the hull.
const GRID =
  'grid grid-cols-[repeat(2,minmax(0,62px))_minmax(20px,28px)_repeat(2,minmax(0,62px))] items-center justify-center gap-x-1.5 sm:gap-x-2'

const SeatButton = memo(function SeatButton({
  seat,
  selected,
  onSelect,
}: {
  seat: Seat
  selected: boolean
  onSelect: (id: string) => void
}) {
  const sold = seat.status === 'sold'
  const held = seat.status === 'held'
  const unavailable = sold || held
  const surcharge = seat.priceMultiplier > 1
  const tags = seatTags(seat)
  const state = sold ? t('sold') : held ? t('held by another traveler') : ''

  return (
    <div className="relative">
      {surcharge && (
        <span
          className={`absolute -left-1 -top-1 z-10 flex h-4 w-4 items-center justify-center rounded-full text-white ${
            unavailable ? 'bg-outline' : 'bg-signal-amber'
          }`}
        >
          <Icon name="workspace_premium" className="text-[11px]" />
        </span>
      )}
      {seat.window && (
        <span
          className={`absolute -right-1 -top-1 z-10 flex h-4 w-4 items-center justify-center rounded-full text-[9px] font-bold text-white ${
            unavailable ? 'bg-outline' : 'bg-sky-aqua'
          }`}
        >
          {t('W')}
        </span>
      )}
      {selected && (
        <span className="absolute -top-2 left-1/2 z-10 -translate-x-1/2 rounded bg-signal-amber px-1 text-[8px] font-bold uppercase text-deep-river">
          {t('Selected')}
        </span>
      )}
      <button
        type="button"
        disabled={unavailable}
        aria-pressed={selected}
        aria-label={`${t('Seat {id}', { id: seat.code })}${tags ? `, ${tags}` : ''}${
          state ? `, ${state}` : ''
        }`}
        onClick={() => onSelect(seat.id)}
        className={`flex h-[46px] w-full flex-col items-center justify-center rounded-lg border-2 text-[11px] font-semibold transition-all ${
          selected
            ? 'border-teal-flow bg-teal-flow text-white shadow-md'
            : sold
            ? 'cursor-not-allowed border-transparent bg-outline-variant/50 text-outline'
            : held
            ? 'cursor-not-allowed border-dashed border-signal-amber bg-signal-amber/15 text-deep-river/70'
            : surcharge
            ? 'border-signal-amber/60 bg-signal-amber/10 text-deep-river hover:border-signal-amber hover:bg-signal-amber/20'
            : 'border-teal-flow/40 bg-white text-deep-river hover:border-teal-flow hover:bg-sand-light'
        }`}
      >
        <Icon name={held ? 'hourglass_top' : 'airline_seat_recline_normal'} className="text-[16px]" />
        {seat.code}
      </button>
    </div>
  )
})

export function SeatLegend() {
  return (
    <div className="flex flex-wrap items-center gap-space-md text-xs text-on-surface-variant">
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded border-2 border-teal-flow/40 bg-white text-[8px] font-bold text-deep-river">
          {t('A1')}
        </span>
        {t('Available')}
      </span>
      <span className="flex items-center gap-1.5 font-semibold text-deep-river">
        <span className="flex h-5 w-5 items-center justify-center rounded bg-teal-flow text-[8px] font-bold text-white">
          {t('A2')}
        </span>
        {t('Selected')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded border-2 border-dashed border-signal-amber bg-signal-amber/15 text-deep-river/70">
          <Icon name="hourglass_top" className="text-[12px]" />
        </span>
        {t('Held by another traveler')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded bg-outline-variant/50 text-outline">
          <Icon name="block" className="text-[13px]" />
        </span>
        {t('Sold')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded-full border border-sky-aqua bg-sky-aqua/20">
          <span className="h-2 w-2 rounded-full bg-sky-aqua" />
        </span>
        {t('Window River View')}
      </span>
    </div>
  )
}

function CabinHeading({ category, feeVnd }: { category: SeatCategory; feeVnd: number }) {
  return (
    <div className="flex items-center gap-2 pt-1 text-[10px] font-bold uppercase tracking-wider text-teal-flow">
      <span className="h-px flex-1 bg-teal-flow/30" />
      {cabinLabel(category)}
      {feeVnd > 0 && (
        <span className="text-signal-amber">• +{formatVndSuffix(feeVnd)}</span>
      )}
      <span className="h-px flex-1 bg-teal-flow/30" />
    </div>
  )
}

/** Top-down plan of the boat: front cabin, standard cabin and outdoor deck, seats A-B | aisle | C-D. */
export default function SeatMapView({ seatMap, selectedIds, fareVnd, onSelect }: SeatMapViewProps) {
  const cabins = useMemo(
    () =>
      CABIN_ORDER.map((category) => {
        const seats = seatMap.seats.filter((s) => s.category === category)
        const rows = [...new Set(seats.map((s) => s.row))].sort((a, b) => a - b)
        return { category, seats, rows, feeVnd: seatFeeFor(seats[0], fareVnd) }
      }).filter((cabin) => cabin.seats.length > 0),
    [seatMap.seats, fareVnd],
  )

  return (
    <div className="relative overflow-hidden rounded-2xl border border-teal-flow/20 bg-gradient-to-b from-[#E0F2F5] via-[#EBF7F8] to-[#E3EFF3] p-space-sm sm:p-space-lg">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(#147a7e_1px,transparent_1px)] opacity-30 [background-size:18px_18px]" />
      <div className="pointer-events-none absolute inset-y-8 left-4 hidden w-4 rounded-full bg-teal-flow/30 md:block" />
      <div className="pointer-events-none absolute inset-y-8 right-4 hidden w-4 rounded-full bg-teal-flow/30 md:block" />

      <div className="relative mx-auto max-w-sm rounded-t-[80px] rounded-b-3xl border border-outline-variant/40 bg-white px-space-sm pb-space-md pt-space-lg shadow-lg sm:px-space-md">
        <div className="mx-auto mb-space-sm flex w-fit items-center gap-2 rounded-full border border-outline-variant/40 bg-white px-4 py-1.5 text-[11px] font-bold uppercase tracking-wider text-deep-river shadow-sm">
          <Icon name="navigation" className="text-[14px]" />
          {t('Forward / Bow • River Cruise Direction')}
        </div>

        <div className="mb-2 flex items-center justify-between text-[10px] font-semibold uppercase tracking-wider text-teal-flow">
          <span className="flex items-center gap-1">
            <Icon name="waves" className="text-[13px]" /> {t('Portside Panoramic Window')}
          </span>
          <span className="flex items-center gap-1">
            {t('Starboard Window')} <Icon name="waves" className="text-[13px]" />
          </span>
        </div>
        <div
          className={`${GRID} text-center text-[10px] font-semibold text-on-surface-variant`}
        >
          <span>{t('Port A')}</span>
          <span>{t('Aisle B')}</span>
          <span className="flex items-center justify-center">
            <Icon name="keyboard_double_arrow_down" className="text-[12px]" />
          </span>
          <span>{t('Aisle C')}</span>
          <span>{t('Starboard D')}</span>
        </div>

        <div className="mt-2 space-y-3">
          {cabins.map(({ category, seats, rows, feeVnd }) => {
            const seatAt = (row: number, column: Seat['column']) =>
              seats.find((s) => s.row === row && s.column === column)
            const renderSeat = (row: number, column: Seat['column']) => {
              const seat = seatAt(row, column)
              return seat ? (
                <SeatButton
                  key={column}
                  seat={seat}
                  selected={selectedIds.includes(seat.id)}
                  onSelect={onSelect}
                />
              ) : (
                <span key={column} />
              )
            }
            return (
              <div key={category} className="space-y-3">
                <CabinHeading category={category} feeVnd={feeVnd} />
                {rows.map((row) => (
                  <div key={row} className={GRID}>
                    {COLUMNS_LEFT.map((c) => renderSeat(row, c))}
                    <span className="flex h-6 items-center justify-center rounded bg-mist text-[9px] font-bold text-outline">
                      {t('R{row}', { row })}
                    </span>
                    {COLUMNS_RIGHT.map((c) => renderSeat(row, c))}
                  </div>
                ))}
              </div>
            )
          })}
        </div>

        <div className="mt-space-md flex items-center gap-2 border-t border-dashed border-outline-variant pt-space-md text-[11px] font-semibold text-on-surface-variant">
          <span className="flex flex-1 items-center justify-center gap-1 rounded-lg border border-outline-variant/40 bg-mist px-2 py-2">
            <Icon name="luggage" className="text-[15px]" /> {t('Luggage Compartment')}
          </span>
          <span className="flex flex-1 items-center justify-center gap-1 rounded-lg border border-outline-variant/40 bg-mist px-2 py-2">
            <Icon name="door_open" className="text-[15px] text-signal-amber" />{' '}
            {t('Stern Boarding Gate / Entry')}
          </span>
        </div>
        <div className="mt-2 flex items-center justify-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-teal-flow">
          <Icon name="arrow_downward" className="text-[12px]" />
          {t('Aft Gangway • Disembarkation Point')}
          <Icon name="arrow_downward" className="text-[12px]" />
        </div>
      </div>
    </div>
  )
}
