import { useState } from 'react'
import LiveTripMap from '../../components/map/LiveTripMap'
import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import { Itinerary, ModeToggle, SidePanel } from '../../features/tracking/components/TrackingParts'
import { getTrackingData } from '../../features/tracking/services/trackingService'
import type { TrackingMode } from '../../features/tracking/types'
import { useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function LiveTrackingPage() {
  useDocumentTitle('Live Trip Tracking')
  const { data, loading, error, retry } = useFetch(getTrackingData)
  const [mode, setMode] = useState<TrackingMode>('sightseeing')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !data) return <PageLoader />

  const activeId = selectedId ?? data.activePoiId
  const activePoi = data.pois.find((p) => p.id === activeId) ?? data.pois[0]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Live Trip' },
        ]}
      />

      <div className="mt-space-md flex flex-col justify-between gap-space-md lg:flex-row lg:items-end">
        <div className="max-w-2xl">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river">
              {t('Your Journey in Real Time')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
              <span className="h-2 w-2 animate-pulse rounded-full bg-teal-flow" />
              {data.trip.statusLabel}
            </span>
          </div>
          <span className="mt-space-sm inline-flex items-center gap-1.5 rounded-full bg-deep-river px-3 py-1 text-xs font-semibold text-white">
            <Icon name="directions_boat" className="text-[15px]" />
            {t('Trip {code}', { code: data.trip.code })}
          </span>
          <p className="mt-space-sm text-body-md text-on-surface-variant">
            {t(
              'Follow your catamaran crossing along the central Saigon River with real-time positioning, contextual river heritage stories, and synchronized audio commentary.',
            )}
          </p>
        </div>
        <ModeToggle mode={mode} onMode={setMode} />
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-xl lg:grid-cols-12">
        <div className="lg:col-span-8">
          <LiveTripMap
            pois={data.pois}
            activePoiId={activeId}
            mode={mode}
            remainingDetail={data.trip.remainingDetail}
            onSelectPoi={setSelectedId}
          />
        </div>
        <div className="lg:col-span-4">
          <SidePanel trip={data.trip} poi={activePoi} languages={data.languages} mode={mode} />
        </div>
      </div>

      <div className="mt-space-xl">
        <Itinerary pois={data.pois} activePoiId={activeId} onSelect={setSelectedId} />
      </div>
    </div>
  )
}
