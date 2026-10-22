import { formatVndSuffix } from '../../../utils/format'
import type { DateOption } from '../types'
import { t } from '../../../i18n'

interface DateStripProps {
  dates: DateOption[]
  selectedId: string
  tripCount: number
  /** Số chuyến còn bán của từng ngày (theo id ngày); ngày chưa có số thì không hiện. */
  counts?: Record<string, number>
  onSelect: (id: string) => void
}

export default function DateStrip({ dates, selectedId, tripCount, counts, onSelect }: DateStripProps) {
  return (
    <div className="scroll-strip flex items-stretch gap-2 overflow-x-auto p-1">
      {dates.map((date) => {
        const selected = date.id === selectedId
        const count = counts?.[date.id] ?? (selected ? tripCount : undefined)
        return (
          <button
            key={date.id}
            type="button"
            aria-pressed={selected}
            onClick={() => onSelect(date.id)}
            className={`flex min-h-[88px] min-w-[108px] shrink-0 flex-col items-center justify-center gap-1 rounded-xl border px-3 py-3 transition-colors ${
              selected
                ? 'border-deep-river bg-deep-river text-on-primary shadow-sm ring-2 ring-teal-flow/30'
                : 'border-deep-river/10 bg-white text-on-surface-variant shadow-xs hover:bg-sand-light/60'
            }`}
          >
            <span
              className={`text-[11px] uppercase ${
                selected ? 'font-bold tracking-wide text-sky-aqua' : 'font-semibold text-outline'
              }`}
            >
              {date.isToday ? t('Today • {weekday}', { weekday: date.weekday }) : date.weekday}
            </span>
            <span
              className={`font-headline-sm text-sm font-bold ${
                selected ? 'text-on-primary' : 'text-deep-river'
              }`}
            >
              {date.label}
            </span>
            {selected ? (
              <span className="mt-0.5 inline-flex items-center rounded-full bg-teal-flow px-2 py-0.5 text-[11px] font-semibold text-white">
                {count === 0 ? t('No trips') : t('{tripCount} Trips', { tripCount: count ?? tripCount })}
              </span>
            ) : count !== undefined ? (
              <span
                className={`text-[11px] font-medium ${count === 0 ? 'text-outline' : 'text-teal-flow'}`}
              >
                {count === 0 ? t('No trips') : t('{tripCount} Trips', { tripCount: count })}
              </span>
            ) : (
              date.fromFareVnd !== undefined && (
                <span className="text-[11px] font-medium text-teal-flow">
                  {t('From {price}', { price: formatVndSuffix(date.fromFareVnd) })}
                </span>
              )
            )}
          </button>
        )
      })}
    </div>
  )
}
