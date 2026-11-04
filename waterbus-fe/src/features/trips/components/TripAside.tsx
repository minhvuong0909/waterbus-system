import { lazy, Suspense } from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { CorridorMap, TripDetail } from '../types'

// Leaflet is heavy, so the map loads after the rest of the trip page.
const PierMap = lazy(() => import('./PierMap'))

const MAP_PLACEHOLDER = 'h-[320px] rounded-2xl bg-surface-container sm:h-[380px]'

/** Cột phải của trang chi tiết chuyến: bản đồ thật các bến trên đoạn khách đi (dữ liệu bến từ API). */
export default function TripAside({ trip, map }: { trip: TripDetail; map: CorridorMap | undefined }) {
  return (
    <aside className="lg:sticky lg:top-24 lg:col-span-5">
      <div className="rounded-2xl bg-surface-container-lowest p-space-md shadow-card">
        <div className="mb-space-sm flex items-center justify-between gap-2 text-body-sm">
          <span className="flex items-center gap-1 font-semibold text-deep-river">
            <Icon name="map" className="text-base text-teal-flow" />
            {t('Route map')}
          </span>
          <span className="truncate text-on-surface-variant">
            {trip.originPierName} → {trip.destinationPierName}
          </span>
        </div>
        {map ? (
          <Suspense fallback={<div className={MAP_PLACEHOLDER} />}>
            <PierMap compact map={map} />
          </Suspense>
        ) : (
          <div aria-hidden className={`${MAP_PLACEHOLDER} animate-pulse motion-reduce:animate-none`} />
        )}
      </div>
    </aside>
  )
}
