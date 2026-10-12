import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useSearchQuery } from '../hooks/useTripSearch'
import type { DateOption, PierOption, TripType } from '../types'
import { formatDateLabel } from '../utils'
import PierField from './PierField'
import { t } from '../../../i18n'

interface SearchFormProps {
  piers: PierOption[]
  dates: DateOption[]
}

const TRIP_TYPES: { id: TripType; label: string }[] = [
  { id: 'one-way', label: 'One-way Crossing' },
  { id: 'round-trip', label: 'Round-trip Cruise' },
]

function StaticField({
  icon,
  label,
  value,
  note,
}: {
  icon: string
  label: string
  value: string
  note: string
}) {
  return (
    <div className="group flex flex-col justify-between rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] p-3.5 transition-colors hover:bg-[#EBF2F0]/80">
      <div className="mb-1 flex items-center justify-between">
        <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-on-surface-variant">
          <Icon name={icon} className="text-[15px] text-teal-flow" />
          {label}
        </span>
        <Icon name="expand_more" className="text-[16px] text-outline" />
      </div>
      <div>
        <span className="block truncate font-headline-sm text-base font-bold text-deep-river">
          {value}
        </span>
        <p className="mt-0.5 truncate text-[11px] text-on-surface-variant">{note}</p>
      </div>
    </div>
  )
}

export default function SearchForm({ piers, dates }: SearchFormProps) {
  const navigate = useNavigate()
  const { query, updateQuery } = useSearchQuery()
  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  const date = dates.find((d) => d.id === query.dateId) ?? dates[0]

  // A trip needs two different piers: picking the other field's pier swaps them instead.
  const chooseOrigin = (id: string) =>
    updateQuery(
      id === query.destinationId
        ? { originId: id, destinationId: query.originId }
        : { originId: id },
    )
  const chooseDestination = (id: string) =>
    updateQuery(
      id === query.originId
        ? { destinationId: id, originId: query.destinationId }
        : { destinationId: id },
    )

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    navigate(ROUTES.searchResults)
  }

  return (
    <div className="rounded-2xl border border-[#E2ECEE] bg-white p-6 shadow-[0_4px_24px_rgba(13,37,56,0.06)] sm:p-7">
      <div className="mb-5 flex items-center justify-between">
        <div
          className="inline-flex rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] p-1"
          role="tablist"
        >
          {TRIP_TYPES.map((type) => (
            <button
              key={type.id}
              type="button"
              role="tab"
              aria-selected={query.tripType === type.id}
              onClick={() => updateQuery({ tripType: type.id })}
              className={`rounded-lg px-5 py-1.5 font-headline-sm text-xs font-semibold transition-all ${
                query.tripType === type.id
                  ? 'bg-deep-river text-on-primary shadow-sm'
                  : 'text-on-surface-variant hover:text-deep-river'
              }`}
            >
              {t(type.label)}
            </button>
          ))}
        </div>
        <span className="hidden items-center gap-1.5 text-xs font-medium text-on-surface-variant md:inline-flex">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Direct Pier-to-Pier Water Transit Corridor')}
        </span>
      </div>

      <form
        onSubmit={handleSubmit}
        className="grid grid-cols-1 items-stretch gap-3 md:grid-cols-2 lg:grid-cols-12"
      >
        <PierField
          className="lg:col-span-3"
          label={t('Departure Pier')}
          dotClass="bg-emerald-500 ring-4 ring-emerald-100"
          value={pier(query.originId)}
          options={piers}
          onChange={chooseOrigin}
        />
        <div className="z-10 hidden items-center justify-center lg:col-span-1 lg:-mx-3 lg:flex">
          <button
            type="button"
            aria-label={t('Swap origin and destination')}
            onClick={() =>
              updateQuery({ originId: query.destinationId, destinationId: query.originId })
            }
            className="group flex h-10 w-10 items-center justify-center rounded-full border border-[#E2ECEE] bg-white text-deep-river shadow-sm transition-all hover:bg-teal-flow hover:text-on-primary"
          >
            <Icon
              name="swap_horiz"
              className="text-[18px] transition-transform group-hover:rotate-180"
            />
          </button>
        </div>
        <PierField
          className="lg:col-span-3"
          label={t('Arrival Pier')}
          dotClass="bg-teal-flow ring-4 ring-teal-100"
          value={pier(query.destinationId)}
          options={piers}
          onChange={chooseDestination}
        />
        <div className="lg:col-span-2">
          <StaticField
            icon="calendar_today"
            label={t('Date')}
            value={formatDateLabel(date)}
            note={t('Service Every 15 min')}
          />
        </div>
        <div className="lg:col-span-2">
          <StaticField
            icon="person"
            label={t('Passengers & Class')}
            value={t(query.passengers)}
            note={t(query.passengerNote)}
          />
        </div>
        <div className="flex items-stretch lg:col-span-1">
          <button
            type="submit"
            className="group flex min-h-[58px] w-full items-center justify-center gap-1 rounded-xl bg-teal-flow font-headline-sm text-sm font-bold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.28)] transition-all hover:bg-secondary hover:shadow-[0_4px_16px_rgba(20,122,126,0.38)]"
          >
            <span>{t('Search')}</span>
            <Icon
              name="arrow_forward"
              className="text-[18px] transition-transform group-hover:translate-x-0.5"
            />
          </button>
        </div>
      </form>
    </div>
  )
}
