import { Icon } from '../../../components/ui'
import type { TimeBand, TripKind } from '../types'
import { t } from '../../../i18n'

export type TimeFilter = 'all' | TimeBand
export type SortKey = 'time' | 'speed' | 'price'
export type KindFilter = 'all' | TripKind

const TIME_FILTERS: { id: TimeFilter; label: string }[] = [
  { id: 'all', label: 'All Times' },
  { id: 'morning', label: 'Morning' },
  { id: 'afternoon', label: 'Afternoon' },
  { id: 'sunset', label: 'Sunset' },
]

const KIND_FILTERS: { id: KindFilter; label: string }[] = [
  { id: 'all', label: 'All Types' },
  { id: 'transit', label: 'Regular' },
  { id: 'sightseeing', label: 'Sightseeing' },
]

const SORTS: { id: SortKey; label: string }[] = [
  { id: 'time', label: 'Earliest Departure' },
  { id: 'speed', label: 'Fastest Crossing' },
  { id: 'price', label: 'Lowest Fare' },
]

interface ResultFiltersProps {
  time: TimeFilter
  kind: KindFilter
  sort: SortKey
  onTime: (time: TimeFilter) => void
  onKind: (kind: KindFilter) => void
  onSort: (s: SortKey) => void
}

const selectClass =
  'min-h-[40px] min-w-0 flex-1 cursor-pointer rounded-lg border-0 bg-transparent py-2 pl-1 pr-8 text-sm font-semibold text-deep-river focus:ring-2 focus:ring-teal-flow/30'
const boxClass =
  'flex min-w-0 items-center gap-1.5 rounded-xl border border-deep-river/10 bg-white px-3 shadow-sm transition-colors focus-within:border-teal-flow/50 hover:border-teal-flow/30'

export default function ResultFilters({
  time,
  kind,
  sort,
  onTime,
  onKind,
  onSort,
}: ResultFiltersProps) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-space-md pt-1">
      <div className="scroll-strip flex max-w-full items-center gap-1.5 overflow-x-auto py-1">
        {TIME_FILTERS.map((f) => (
          <button
            key={f.id}
            type="button"
            aria-pressed={time === f.id}
            onClick={() => onTime(f.id)}
            className={`min-h-[40px] shrink-0 rounded-full border border-transparent px-3.5 py-2 text-xs transition-colors ${
              time === f.id
                ? 'bg-deep-river font-semibold text-on-primary'
                : 'border border-deep-river/10 bg-white font-medium text-on-surface-variant shadow-xs hover:bg-sand-light/50 hover:text-deep-river'
            }`}
          >
            {t(f.label)}
          </button>
        ))}
      </div>
      <div className="grid w-full min-w-0 grid-cols-1 gap-2 sm:w-auto sm:grid-cols-2">
        <div className={boxClass}>
          <span className="text-xs text-outline">{t('Trip type:')}</span>
          <select
            aria-label={t('Trip type:')}
            className={selectClass}
            value={kind}
            onChange={(e) => onKind(e.target.value as KindFilter)}
          >
            {KIND_FILTERS.map((k) => (
              <option key={k.id} value={k.id}>
                {t(k.label)}
              </option>
            ))}
          </select>
        </div>
        <div className={boxClass}>
          <Icon name="swap_vert" className="text-[15px] text-teal-flow" />
          <span className="text-xs text-outline">{t('Sort:')}</span>
          <select
            aria-label={t('Sort:')}
            className={selectClass}
            value={sort}
            onChange={(e) => onSort(e.target.value as SortKey)}
          >
            {SORTS.map((s) => (
              <option key={s.id} value={s.id}>
                {t(s.label)}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  )
}
