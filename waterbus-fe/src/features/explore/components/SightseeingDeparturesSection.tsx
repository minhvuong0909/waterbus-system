import { Link } from 'react-router-dom'
import { ErrorState, Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip, setQuery } from '../../booking/bookingSlice'
import DepartureCard from '../../trips/components/DepartureCard'
import { SECTION, SIGHTSEEING_RESULTS } from '../constants'
import type { SightseeingDeparture } from '../types'

interface SightseeingDeparturesSectionProps {
  departures: SightseeingDeparture[] | undefined
  error: boolean
  onRetry: () => void
}

/** Các chuyến ngắm cảnh thật sắp chạy (từ API tìm chuyến), chọn một chuyến là mở chi tiết chuyến đó. */
export default function SightseeingDeparturesSection({
  departures,
  error,
  onRetry,
}: SightseeingDeparturesSectionProps) {
  const dispatch = useAppDispatch()

  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <div className="mb-space-lg flex flex-col justify-between gap-space-sm md:flex-row md:items-end">
          <h2 className="font-display text-headline-lg tracking-tight text-deep-river">
            {t('Upcoming sightseeing trips')}
          </h2>
          <Link
            to={SIGHTSEEING_RESULTS}
            className="tap-area inline-flex items-center gap-0.5 text-sm font-bold text-teal-flow hover:underline"
          >
            {t('View the full timetable')}
            <Icon name="chevron_right" className="text-base" />
          </Link>
        </div>

        {error ? (
          <ErrorState compact onRetry={onRetry} />
        ) : !departures ? (
          <div aria-hidden className="grid grid-cols-1 gap-space-sm md:grid-cols-2">
            {[0, 1].map((i) => (
              <div
                key={i}
                className="h-20 animate-pulse rounded-2xl bg-surface-container motion-reduce:animate-none"
              />
            ))}
          </div>
        ) : departures.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-lg text-center text-sm text-on-surface-variant">
            {t('No sightseeing trips can be booked in the next few days.')}
          </p>
        ) : (
          <div data-reveal className="grid grid-cols-1 gap-space-sm md:grid-cols-2">
            {departures.map(({ departure, search }) => (
              <DepartureCard
                key={departure.id}
                departure={departure}
                onSelect={(trip) => {
                  // The trip page reads the piers and the day from the search, so set them first.
                  dispatch(setQuery(search))
                  dispatch(selectTrip(trip))
                }}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  )
}
