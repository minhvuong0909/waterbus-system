import { lazy, Suspense } from 'react'
import { Link } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { setQuery } from '../../booking/bookingSlice'
import type { CorridorMap } from '../../trips/types'
import { t } from '../../../i18n'

// The map pulls in Leaflet and its styles, so it loads after the rest of the page.
const PierMap = lazy(() => import('../../../components/map/PierMap'))

interface CorridorSectionProps {
  map: CorridorMap
}

/** Bản đồ các bến trên hành lang sông: thông tin tham khảo, đặt thấp trong trang. */
export default function CorridorSection({ map }: CorridorSectionProps) {
  const dispatch = useAppDispatch()

  return (
    <section id="map" className="w-full scroll-mt-20 bg-mist py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-6 px-5 sm:px-8">
        <h2
          data-reveal
          className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]"
        >
          {t('River corridor map')}
        </h2>
        <div
          data-reveal
          className="rounded-panel border border-outline-variant/30 bg-white p-4 shadow-sm sm:p-5"
        >
          <Suspense
            fallback={
              <div className="flex h-[320px] items-center justify-center rounded-2xl bg-surface-container text-sm text-on-surface-variant sm:h-[380px]">
                {t('Loading map…')}
              </div>
            }
          >
            <PierMap
              compact
              map={map}
              pierActions={(pier) => (
                <Link
                  to={ROUTES.search}
                  onClick={() => dispatch(setQuery({ originId: pier.id }))}
                  className="rounded-full bg-teal-flow px-3 py-1.5 text-xs font-semibold text-on-primary hover:bg-secondary"
                >
                  {t('Plan a trip from here')}
                </Link>
              )}
            />
          </Suspense>
        </div>
      </div>
    </section>
  )
}
