import ChoiceDropdown from './ChoiceDropdown'
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
  /** Chỉ cho sắp xếp theo giá khi các chuyến đang xem có giá khác nhau (giá thường như nhau, BR-FARE-02). */
  showPriceSort?: boolean
}

export default function ResultFilters({
  time,
  kind,
  sort,
  onTime,
  onKind,
  onSort,
  showPriceSort = true,
}: ResultFiltersProps) {
  const sorts = SORTS.filter((s) => s.id !== 'price' || showPriceSort)
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
        <ChoiceDropdown
          label={t('Trip type:')}
          options={KIND_FILTERS.map((k) => ({ id: k.id, label: t(k.label) }))}
          value={kind}
          onChange={(id) => onKind(id as KindFilter)}
        />
        <ChoiceDropdown
          label={t('Sort:')}
          icon="swap_vert"
          options={sorts.map((s) => ({ id: s.id, label: t(s.label) }))}
          value={sort}
          onChange={(id) => onSort(id as SortKey)}
        />
      </div>
    </div>
  )
}
