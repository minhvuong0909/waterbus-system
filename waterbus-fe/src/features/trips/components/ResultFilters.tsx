import { Icon } from '../../../components/ui'
import type { TimeBand, Trip } from '../types'
import { t } from '../../../i18n'

export type TimeFilter = 'all' | TimeBand
export type SortKey = 'time' | 'speed' | 'price'

const TIME_FILTERS: { id: TimeFilter; label: string }[] = [
  { id: 'all', label: 'All Times' },
  { id: 'morning', label: 'Morning' },
  { id: 'afternoon', label: 'Afternoon' },
  { id: 'sunset', label: 'Sunset' },
]

const SORTS: { id: SortKey; label: string }[] = [
  { id: 'time', label: 'Earliest Departure' },
  { id: 'speed', label: 'Fastest Crossing' },
  { id: 'price', label: 'Lowest Fare' },
]

interface ResultFiltersProps {
  trips: Trip[]
  time: TimeFilter
  vessel: string
  sort: SortKey
  onTime: (time: TimeFilter) => void
  onVessel: (v: string) => void
  onSort: (s: SortKey) => void
}

const selectClass =
  'cursor-pointer bg-transparent text-xs font-semibold text-deep-river focus:outline-none'
const boxClass =
  'flex items-center gap-1.5 rounded-full border border-deep-river/10 bg-white px-3 py-1.5 shadow-xs'

export default function ResultFilters({
  trips,
  time,
  vessel,
  sort,
  onTime,
  onVessel,
  onSort,
}: ResultFiltersProps) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-space-md pt-1">
      <div className="flex items-center gap-1.5 overflow-x-auto">
        {TIME_FILTERS.map((f) => (
          <button
            key={f.id}
            type="button"
            aria-pressed={time === f.id}
            onClick={() => onTime(f.id)}
            className={`rounded-full px-3.5 py-1.5 text-xs transition-all ${
              time === f.id
                ? 'bg-deep-river font-semibold text-on-primary'
                : 'border border-deep-river/10 bg-white font-medium text-on-surface-variant shadow-xs hover:bg-sand-light/50 hover:text-deep-river'
            }`}
          >
            {t(f.label)}
          </button>
        ))}
      </div>
      <div className="flex items-center gap-space-sm">
        <div className={boxClass}>
          <span className="text-xs text-outline">{t('Vessel:')}</span>
          <select className={selectClass} value={vessel} onChange={(e) => onVessel(e.target.value)}>
            <option value="all">{t('All Vessels ({length})', { length: trips.length })}</option>
            {trips.map((trip) => (
              <option key={trip.id} value={trip.id}>
                {trip.vesselName} ({trip.vesselCode})
              </option>
            ))}
          </select>
        </div>
        <div className={boxClass}>
          <Icon name="swap_vert" className="text-[15px] text-teal-flow" />
          <span className="text-xs text-outline">{t('Sort:')}</span>
          <select
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
