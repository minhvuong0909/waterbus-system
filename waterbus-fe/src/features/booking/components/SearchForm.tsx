import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useSearchQuery } from '../hooks/useTripSearch'
import type { DateOption, PierOption } from '../../trips/types'
import DateField from './DateField'
import PierField from './PierField'
import { t } from '../../../i18n'

interface SearchFormProps {
  piers: PierOption[]
  dates: DateOption[]
  /** `bar`: thanh một hàng đặt đè lên mép hero; `card`: khối đầy đủ ở trang tìm chuyến. */
  variant?: 'card' | 'bar'
}

export default function SearchForm({ piers, dates, variant = 'card' }: SearchFormProps) {
  const bar = variant === 'bar'
  const navigate = useNavigate()
  const { query, updateQuery } = useSearchQuery()
  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  const dateId = (dates.find((d) => d.id === query.dateId) ?? dates[0]).id
  const roundTrip = query.tripType === 'round-trip'
  // The return day is never before the outbound day (BR-SRCH-01).
  const returnDates = dates.filter((d) => d.id >= dateId)
  const returnDateId = (returnDates.find((d) => d.id === query.returnDateId) ?? returnDates[0])?.id ?? dateId

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
    <div
      className={
        bar
          ? 'rounded-2xl border border-white/70 bg-white p-2.5 shadow-overlay'
          : 'rounded-2xl border border-outline-variant/50 bg-white p-4 shadow-raised sm:p-6'
      }
    >
      {!bar && (
        <p className="mb-4 flex items-center gap-1.5 text-xs font-medium text-on-surface-variant">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('Direct Pier-to-Pier Water Transit Corridor')}
        </p>
      )}

      <div
        role="radiogroup"
        aria-label={t('Ticket type')}
        className={`inline-flex rounded-full bg-surface-container p-1 ${bar ? 'mb-2' : 'mb-3'}`}
      >
        {(['one-way', 'round-trip'] as const).map((type) => (
          <button
            key={type}
            type="button"
            role="radio"
            aria-checked={query.tripType === type}
            onClick={() =>
              updateQuery(
                type === 'round-trip'
                  ? { tripType: type, returnDateId: query.returnDateId ?? dateId }
                  : { tripType: type },
              )
            }
            className={`min-h-[36px] rounded-full px-4 text-sm transition-colors ${
              query.tripType === type
                ? 'bg-deep-river font-semibold text-white'
                : 'text-on-surface-variant hover:text-deep-river'
            }`}
          >
            {type === 'round-trip' ? t('Round trip') : t('One way')}
          </button>
        ))}
      </div>

      <form
        onSubmit={handleSubmit}
        className={`grid grid-cols-1 items-stretch md:grid-cols-2 ${
          bar
            ? roundTrip
              ? 'gap-2 xl:grid-cols-[minmax(0,1.4fr)_40px_minmax(0,1.4fr)_minmax(0,1.9fr)_auto]'
              : 'gap-2 xl:grid-cols-[minmax(0,1.5fr)_40px_minmax(0,1.5fr)_minmax(0,1.1fr)_auto]'
            : roundTrip
              ? 'gap-3 xl:grid-cols-[minmax(0,1.4fr)_44px_minmax(0,1.4fr)_minmax(0,2fr)_minmax(0,0.9fr)]'
              : 'gap-3 xl:grid-cols-[minmax(0,1.5fr)_44px_minmax(0,1.5fr)_minmax(0,1.2fr)_minmax(0,0.9fr)]'
        }`}
      >
        {/* Below xl the two piers sit in their own grid and the swap button floats between them;
            from xl the wrapper disappears and the button takes its own grid column. */}
        <div className="relative grid grid-cols-1 gap-2 md:col-span-2 md:grid-cols-2 xl:contents">
        <PierField
          className="min-w-0"
          label={t('Departure Pier')}
          dotClass="bg-emerald-500 ring-4 ring-emerald-100"
          value={pier(query.originId)}
          options={piers}
          onChange={chooseOrigin}
          compact={bar}
        />
        <button
          type="button"
          aria-label={t('Swap origin and destination')}
          onClick={() =>
            updateQuery({ originId: query.destinationId, destinationId: query.originId })
          }
          className="group absolute right-3 top-1/2 z-10 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full border border-outline-variant/50 bg-white text-deep-river shadow-sm transition-all before:absolute before:-inset-1 before:content-[''] hover:bg-teal-flow hover:text-on-primary md:left-1/2 md:right-auto md:-translate-x-1/2 xl:static xl:h-10 xl:w-10 xl:translate-x-0 xl:translate-y-0 xl:self-center xl:justify-self-center"
        >
          <Icon
            name="swap_horiz"
            className="rotate-90 text-lg transition-transform group-hover:rotate-[270deg] md:rotate-0 md:group-hover:rotate-180"
          />
        </button>
        <PierField
          className="min-w-0"
          label={t('Arrival Pier')}
          dotClass="bg-teal-flow ring-4 ring-teal-100"
          value={pier(query.destinationId)}
          options={piers}
          onChange={chooseDestination}
          compact={bar}
        />
        </div>
        <div className={`min-w-0 md:col-span-2 xl:col-span-1 ${roundTrip ? 'grid grid-cols-2 gap-2' : ''}`}>
          <DateField
            dates={dates}
            value={dateId}
            onChange={(id) => updateQuery({ dateId: id })}
            compact={bar}
            label={roundTrip ? t('Outbound date') : undefined}
          />
          {roundTrip && (
            <DateField
              dates={returnDates}
              value={returnDateId}
              onChange={(id) => updateQuery({ returnDateId: id })}
              compact={bar}
              label={t('Return date')}
            />
          )}
        </div>
        <div className="flex items-stretch md:col-span-2 xl:col-span-1">
          <button
            type="submit"
            className={`group flex w-full items-center justify-center gap-1 rounded-xl bg-teal-flow font-display text-sm font-bold text-on-primary ${bar ? 'min-h-[64px] px-8' : 'min-h-[58px]'}  shadow-glow transition-all hover:bg-secondary hover:shadow-glow`}
          >
            <span>{t('Find trips')}</span>
            <Icon
              name="arrow_forward"
              className="text-lg transition-transform group-hover:translate-x-0.5"
            />
          </button>
        </div>
      </form>
    </div>
  )
}
