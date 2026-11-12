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

/**
 * Các chuyến ngắm cảnh thật sắp chạy (từ API tìm chuyến); chọn một chuyến là vào chọn ghế của chuyến
 * đó. Khi không có chuyến nào đặt được thì cả khối ẩn đi thay vì chiếm chỗ bằng một dòng thông báo.
 */
export default function SightseeingDeparturesSection({
  departures,
  error,
  onRetry,
}: SightseeingDeparturesSectionProps) {
  const dispatch = useAppDispatch()

  if (departures?.length === 0 && !error) return null

  return (
    <section className="bg-surface pt-space-3xl">
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
        ) : (
          <div data-reveal className="grid grid-cols-1 gap-space-sm md:grid-cols-2">
            {departures.map(({ departure, search }) => (
              <DepartureCard
                key={departure.id}
                departure={departure}
                onSelect={(trip) => {
                  // Seat selection reads the piers and the day from the search, so set them first.
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
