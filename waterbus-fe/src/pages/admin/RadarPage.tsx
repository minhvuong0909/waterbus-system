import { useMemo } from 'react'
import FleetMap from '../../components/map/FleetMap'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { PageHeader, Panel, StatusBadge } from '../../features/admin/components/AdminUi'
import { useNow, useReload, useTicker } from '../../features/admin/hooks/useAdmin'
import { useLiveFleet } from '../../features/admin/hooks/useFleetHub'
import {
  getBoats,
  getFleetPositions,
  getRoutes,
  getStations,
  getTrips,
  getWeatherAlerts,
} from '../../features/admin/services/adminService'
import { getFleetBoats, knotsToKmh } from '../../features/admin/services/operationsService'
import type { WeatherAlert } from '../../features/admin/types'
import { formatClockTime, formatClockTimeWithSeconds, routeStations, weatherAlertTitle } from '../../features/admin/utils'
import { USE_MOCK } from '../../constant/env'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import type { RealtimeStatus } from '../../services/realtime'

/** How often the mock boat positions are recomputed. */
const REFRESH_MS = 3000
/** A real position older than this is shown as stale (the boat stopped reporting). */
const STALE_AFTER_MS = 2 * 60_000

function WeatherBanners({ alerts }: { alerts: WeatherAlert[] }) {
  return (
    <>
      {alerts.map((alert) => (
        <div
          key={alert.id}
          role="alert"
          className="mb-4 flex items-center gap-3 rounded-xl border border-signal-amber/50 bg-signal-amber/15 px-4 py-3 text-sm text-deep-river"
        >
          <Icon name="air" className="text-[20px] text-signal-amber" />
          <span className="font-semibold">{weatherAlertTitle(alert)}</span>
          <span className="text-on-surface-variant">{t(alert.message)}</span>
        </div>
      ))}
    </>
  )
}

const STATUS_TEXT: Record<RealtimeStatus, string> = {
  connecting: 'Connecting…',
  live: 'Live',
  reconnecting: 'Reconnecting…',
}

function LiveIndicator({ status }: { status: RealtimeStatus }) {
  const color = status === 'live' ? 'bg-teal-flow' : 'bg-signal-amber'
  return (
    <span className="inline-flex items-center gap-2 text-xs font-semibold text-on-surface-variant">
      <span className={`h-2 w-2 rounded-full ${color}`} />
      {t(STATUS_TEXT[status])}
    </span>
  )
}

/** Real radar: boats from the API, positions from GPS / the simulator through the tracking hub. */
function LiveRadar() {
  const [alertVersion, reloadAlerts] = useReload()
  const stations = useFetch(getStations)
  const routes = useFetch(getRoutes)
  const boats = useFetch(getFleetBoats)
  const alerts = useFetch(getWeatherAlerts, [alertVersion])
  const fleet = useLiveFleet(reloadAlerts)
  const now = useNow(15_000) // re-evaluate "stale" labels
  const state = combineFetches(stations, routes, boats, alerts)

  const onRiver = useMemo(
    () =>
      fleet.positions.map((p) => ({
        ...p,
        code: boats.data?.find((b) => b.id === p.boatId)?.code ?? p.boatId.slice(0, 8),
        stale: now - Date.parse(p.observedAt) > STALE_AFTER_MS,
      })),
    [fleet.positions, boats.data, now],
  )

  if (state.error || fleet.error) return <ErrorState onRetry={state.error ? state.retry : fleet.retry} />
  if (state.loading || !fleet.loaded || !stations.data || !routes.data || !boats.data || !alerts.data) {
    return <PageLoader />
  }

  const corridor = routes.data[0] ? routeStations(routes.data[0], stations.data) : stations.data

  return (
    <>
      <PageHeader title="Live Operations" description="Boats on the Saigon River, updated as their GPS reports arrive." />
      <WeatherBanners alerts={alerts.data} />
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="h-[70vh] min-h-[24rem] lg:col-span-2">
          <FleetMap
            stations={corridor}
            boats={onRiver.map((b) => ({ tripId: b.boatId, code: b.code, latitude: b.latitude, longitude: b.longitude }))}
          />
        </div>
        <Panel title="Boats on the river">
          <div className="mb-3">
            <LiveIndicator status={fleet.status} />
          </div>
          {onRiver.length === 0 ? (
            <p className="text-sm text-on-surface-variant">{t('No boat has reported its position yet.')}</p>
          ) : (
            <ul className="space-y-3">
              {onRiver.map((b) => (
                <li key={b.boatId} className="rounded-lg border border-outline-variant/40 p-3">
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-sm font-bold text-deep-river">{b.code}</span>
                    <span className={`text-xs font-semibold ${b.stale ? 'text-signal-amber' : 'text-teal-flow'}`}>
                      {b.stale ? t('No signal') : b.tripId ? t('On a trip') : t('Not on a trip')}
                    </span>
                  </div>
                  <div className="mt-2 flex justify-between text-xs text-on-surface-variant">
                    <span>{t('Updated {time}', { time: formatClockTimeWithSeconds(b.observedAt) })}</span>
                    {knotsToKmh(b.speedKnots) != null && (
                      <span>{t('{speed} km/h', { speed: knotsToKmh(b.speedKnots) ?? 0 })}</span>
                    )}
                  </div>
                  <div className="mt-1 font-mono text-[11px] text-on-surface-variant">
                    {b.latitude.toFixed(5)}, {b.longitude.toFixed(5)}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </>
  )
}

/** Mock radar (`VITE_USE_MOCK`): positions worked out from where each mock trip should be. */
function MockRadar() {
  const tick = useTicker(REFRESH_MS)
  const stations = useFetch(getStations)
  const routes = useFetch(getRoutes)
  const boats = useFetch(getBoats)
  const alerts = useFetch(getWeatherAlerts)
  const positions = useFetch(getFleetPositions, [tick])
  const trips = useFetch(getTrips, [tick])
  const state = combineFetches(stations, routes, boats, alerts, positions, trips)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (
    state.loading ||
    !stations.data ||
    !routes.data ||
    !boats.data ||
    !alerts.data ||
    !positions.data ||
    !trips.data
  ) {
    return <PageLoader />
  }

  const boatCode = (id: string) => boats.data?.find((b) => b.id === id)?.code ?? '—'
  const corridor = routes.data[0] ? routeStations(routes.data[0], stations.data) : stations.data
  const onRiver = positions.data.map((p) => ({
    ...p,
    code: boatCode(p.boatId),
    trip: trips.data?.find((x) => x.id === p.tripId),
  }))

  return (
    <>
      <PageHeader title="Live Operations" description="Boats on the Saigon River, refreshed every few seconds." />
      <WeatherBanners alerts={alerts.data} />
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="h-[70vh] min-h-[24rem] lg:col-span-2">
          <FleetMap
            stations={corridor}
            boats={onRiver.map((b) => ({
              tripId: b.tripId,
              code: b.code,
              latitude: b.latitude,
              longitude: b.longitude,
            }))}
          />
        </div>
        <Panel title="Boats on the river">
          {onRiver.length === 0 ? (
            <p className="text-sm text-on-surface-variant">{t('No boat is sailing right now.')}</p>
          ) : (
            <ul className="space-y-3">
              {onRiver.map((b) => (
                <li key={b.tripId} className="rounded-lg border border-outline-variant/40 p-3">
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-sm font-bold text-deep-river">{b.code}</span>
                    {b.trip && <StatusBadge status={b.trip.status} />}
                  </div>
                  <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-surface-container">
                    <div className="h-full rounded-full bg-teal-flow" style={{ width: `${Math.round(b.progress * 100)}%` }} />
                  </div>
                  <div className="mt-2 flex justify-between text-xs text-on-surface-variant">
                    <span>{t('{percent}% of the route', { percent: Math.round(b.progress * 100) })}</span>
                    <span>{t('{speed} km/h', { speed: b.speedKmh })}</span>
                  </div>
                  {b.trip && (
                    <div className="mt-1 text-xs text-on-surface-variant">
                      {t('Arrives {time}', { time: formatClockTime(b.trip.arrivalTime) })}
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </>
  )
}

export default function RadarPage() {
  useDocumentTitle('Live Operations')
  return USE_MOCK ? <MockRadar /> : <LiveRadar />
}
