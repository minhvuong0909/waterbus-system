import { formatVndSuffix } from '../../../utils/format'
import type { DateOption } from '../types'
import { t } from '../../../i18n'

interface DateStripProps {
  dates: DateOption[]
  selectedId: string
  tripCount: number
  onSelect: (id: string) => void
}

export default function DateStrip({ dates, selectedId, tripCount, onSelect }: DateStripProps) {
  return (
    <div className="flex items-center gap-space-sm overflow-x-auto pb-1">
      {dates.map((date) => {
        const selected = date.id === selectedId
        return (
          <button
            key={date.id}
            type="button"
            aria-pressed={selected}
            onClick={() => onSelect(date.id)}
            className={`flex shrink-0 flex-col items-center rounded-xl px-4 py-2.5 transition-all ${
              selected
                ? 'min-w-[115px] bg-deep-river text-on-primary shadow-sm ring-2 ring-teal-flow/40'
                : 'min-w-[100px] border border-deep-river/10 bg-white text-on-surface-variant shadow-xs hover:bg-sand-light/60'
            }`}
          >
            <span
              className={`text-[10px] uppercase ${
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
              <span className="mt-0.5 inline-flex items-center rounded-full bg-teal-flow px-2 py-0.5 text-[10px] font-semibold text-white">
                {t('{tripCount} Trips', { tripCount })}
              </span>
            ) : (
              <span className="text-[11px] font-medium text-teal-flow">
                {t('From {price}', { price: formatVndSuffix(date.fromFareVnd) })}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
