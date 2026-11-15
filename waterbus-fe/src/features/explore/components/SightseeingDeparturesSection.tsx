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
    <section className="border-b border-outline-variant/30 bg-white py-16 sm:py-20">
      <div className={SECTION}>
        <div className="mb-7 flex flex-col justify-between gap-4 md:flex-row md:items-end">
          <div>
            <p className="mb-2 text-xs font-semibold uppercase tracking-[0.2em] text-teal-flow">
              {t('Make the river your next destination')}
            </p>
            <h2 className="font-display text-3xl font-bold tracking-tight text-deep-river sm:text-4xl lg:text-5xl">
              {t('Upcoming sightseeing trips')}
            </h2>
          </div>
          <Link
            to={SIGHTSEEING_RESULTS}
            className="tap-area inline-flex min-h-11 items-center gap-1 self-start text-sm font-bold text-teal-flow hover:underline md:self-auto"
          >
            {t('View the full timetable')}
            <Icon name="chevron_right" className="text-base" />
          </Link>
        </div>

        {error ? (
          <ErrorState compact onRetry={onRetry} />
        ) : !departures ? (
          <div aria-hidden className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {[0, 1].map((i) => (
              <div
                key={i}
                className="h-20 animate-pulse rounded-2xl bg-surface-container motion-reduce:animate-none"
              />
            ))}
          </div>
        ) : (
          <div data-reveal className="grid grid-cols-1 gap-4 md:grid-cols-2">
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
