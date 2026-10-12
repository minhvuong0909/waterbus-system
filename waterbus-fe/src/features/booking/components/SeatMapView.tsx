import { memo, useMemo } from 'react'
import { Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import type { Seat, SeatMap } from '../types'
import { seatTags } from '../utils'
import { t } from '../../../i18n'

interface SeatMapViewProps {
  seatMap: SeatMap
  selectedId: string
  /** Extra price of a VIP seat for the selected trip. */
  vipFeeVnd: number
  onSelect: (id: string) => void
}

const SeatButton = memo(function SeatButton({
  seat,
  selected,
  onSelect,
}: {
  seat: Seat
  selected: boolean
  onSelect: (id: string) => void
}) {
  const occupied = seat.status === 'occupied'
  const vip = seat.tier === 'vip'
  const tags = seatTags(seat)

  return (
    <div className="relative">
      {vip && (
        <span
          className={`absolute -left-1 -top-1 z-10 flex h-4 w-4 items-center justify-center rounded-full text-white ${
            occupied ? 'bg-outline' : 'bg-signal-amber'
          }`}
        >
          <Icon name="workspace_premium" className="text-[11px]" />
        </span>
      )}
      {seat.window && (
        <span
          className={`absolute -right-1 -top-1 z-10 flex h-4 w-4 items-center justify-center rounded-full text-[9px] font-bold text-white ${
            occupied ? 'bg-outline' : 'bg-sky-aqua'
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
        disabled={occupied}
        aria-pressed={selected}
        aria-label={`${t('Seat {id}', { id: seat.id })}${tags ? `, ${tags}` : ''}${
          occupied ? `, ${t('occupied')}` : ''
        }`}
        onClick={() => onSelect(seat.id)}
        className={`flex h-[46px] w-[62px] flex-col items-center justify-center rounded-lg border-2 text-[11px] font-semibold transition-all ${
          selected
            ? 'border-teal-flow bg-teal-flow text-white shadow-md'
            : occupied
            ? 'cursor-not-allowed border-transparent bg-outline-variant/50 text-outline'
            : vip
            ? 'border-signal-amber/60 bg-signal-amber/10 text-deep-river hover:border-signal-amber hover:bg-signal-amber/20'
            : 'border-teal-flow/40 bg-white text-deep-river hover:border-teal-flow hover:bg-sand-light'
        }`}
      >
        <Icon name="airline_seat_recline_normal" className="text-[16px]" />
        {seat.id}
      </button>
    </div>
  )
})

export function SeatLegend({ vipFeeVnd }: { vipFeeVnd: number }) {
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
        <span className="flex h-5 w-5 items-center justify-center rounded border-2 border-signal-amber/60 bg-signal-amber/10 text-signal-amber">
          <Icon name="workspace_premium" className="text-[13px]" />
        </span>
        {t('VIP (+{fee})', { fee: formatVndSuffix(vipFeeVnd) })}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded-full border border-sky-aqua bg-sky-aqua/20">
          <span className="h-2 w-2 rounded-full bg-sky-aqua" />
        </span>
        {t('Window River View')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded bg-outline-variant/50 text-outline">
          <Icon name="block" className="text-[13px]" />
        </span>
        {t('Occupied')}
      </span>
    </div>
  )
}

/** Top-down cabin plan: 6 rows, seats A-B | aisle | C-D, bow at the top and gangway at the stern. */
export default function SeatMapView({
  seatMap,
  selectedId,
  vipFeeVnd,
  onSelect,
}: SeatMapViewProps) {
  const seatsById = useMemo(() => new Map(seatMap.seats.map((s) => [s.id, s])), [seatMap.seats])
  const seatAt = (row: number, column: Seat['column']) => seatsById.get(`${column}${row}`) as Seat

  return (
    <div className="relative overflow-hidden rounded-2xl border border-teal-flow/20 bg-gradient-to-b from-[#E0F2F5] via-[#EBF7F8] to-[#E3EFF3] p-space-lg">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(#147a7e_1px,transparent_1px)] opacity-30 [background-size:18px_18px]" />
      <div className="pointer-events-none absolute inset-y-8 left-4 hidden w-4 rounded-full bg-teal-flow/30 md:block" />
      <div className="pointer-events-none absolute inset-y-8 right-4 hidden w-4 rounded-full bg-teal-flow/30 md:block" />

      <div className="relative mx-auto max-w-sm rounded-t-[80px] rounded-b-3xl border border-outline-variant/40 bg-white px-space-md pb-space-md pt-space-lg shadow-lg">
        <div className="mx-auto mb-space-sm flex w-fit items-center gap-2 rounded-full border border-outline-variant/40 bg-white px-4 py-1.5 text-[11px] font-bold uppercase tracking-wider text-deep-river shadow-sm">
          <Icon name="navigation" className="text-[14px]" />
          {t('Forward / Bow • River Cruise Direction')}
        </div>
        <div className="mx-auto mb-space-md w-fit rounded-full border border-teal-flow/30 bg-teal-flow/10 px-4 py-1 text-[10px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Wrap-Around Cockpit Bridge Glass')}
        </div>

        <div className="mb-2 flex items-center justify-between text-[10px] font-semibold uppercase tracking-wider text-teal-flow">
          <span className="flex items-center gap-1">
            <Icon name="waves" className="text-[13px]" /> {t('Portside Panoramic Window')}
          </span>
          <span className="flex items-center gap-1">
            {t('Starboard Window')} <Icon name="waves" className="text-[13px]" />
          </span>
        </div>
        <div className="grid grid-cols-[repeat(2,62px)_28px_repeat(2,62px)] justify-center gap-x-2 text-center text-[10px] font-semibold text-on-surface-variant">
          <span>{t('Port A')}</span>
          <span>{t('Aisle B')}</span>
          <span className="flex items-center justify-center">
            <Icon name="keyboard_double_arrow_down" className="text-[12px]" />
          </span>
          <span>{t('Aisle C')}</span>
          <span>{t('Starboard D')}</span>
        </div>

        <div className="mt-2 space-y-3">
          {seatMap.rows.map((row, index) => {
            const startsVip =
              seatAt(row, 'A').tier === 'vip' &&
              seatAt(seatMap.rows[index - 1], 'A')?.tier !== 'vip'
            return (
              <div key={row} className="space-y-3">
                {startsVip && (
                  <div className="flex items-center gap-2 pt-1 text-[10px] font-bold uppercase tracking-wider text-signal-amber">
                    <span className="h-px flex-1 bg-signal-amber/40" />
                    <Icon name="workspace_premium" className="text-[14px]" />
                    {t('VIP Lounge • Priority exit • +{fee}', { fee: formatVndSuffix(vipFeeVnd) })}
                    <span className="h-px flex-1 bg-signal-amber/40" />
                  </div>
                )}
                <div className="grid grid-cols-[repeat(2,62px)_28px_repeat(2,62px)] items-center justify-center gap-x-2">
                  {(['A', 'B'] as const).map((c) => (
                    <SeatButton
                      key={c}
                      seat={seatAt(row, c)}
                      selected={selectedId === `${c}${row}`}
                      onSelect={onSelect}
                    />
                  ))}
                  <span className="flex h-6 items-center justify-center rounded bg-mist text-[9px] font-bold text-outline">
                    {t('R{row}', { row })}
                  </span>
                  {(['C', 'D'] as const).map((c) => (
                    <SeatButton
                      key={c}
                      seat={seatAt(row, c)}
                      selected={selectedId === `${c}${row}`}
                      onSelect={onSelect}
                    />
                  ))}
                </div>
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
