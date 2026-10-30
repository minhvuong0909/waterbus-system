import { useMemo } from 'react'
import FleetMap from '../../components/map/FleetMap'
import { ErrorState, PageLoader } from '../../components/ui'
import { PageHeader, Panel } from '../../features/admin/components/AdminUi'
import { AlertBanner } from '../../features/admin/components/AlertParts'
import { sortAlerts } from '../../features/admin/alertUtils'
import { useNow, useReload } from '../../features/admin/hooks/useAdmin'
import { useLiveFleet } from '../../features/admin/hooks/useFleetHub'
import {
  getRoutes,
  getStations,
  getWeatherAlerts,
} from '../../features/admin/services/adminService'
import { getFleetBoats, knotsToKmh } from '../../features/admin/services/operationsService'
import type { WeatherAlert } from '../../features/admin/types'
import { formatClockTimeWithSeconds, routeStations } from '../../features/admin/utils'
import { riverPathOf } from '../../constant/riverPaths'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'
import type { RealtimeStatus } from '../../services/realtime'

/** A real position older than this is shown as stale (the boat stopped reporting). */
const STALE_AFTER_MS = 2 * 60_000
/**
 * The backend has no route list endpoint yet and seeds a single route, RT01 (the one its GPS
 * simulator also follows). Until the API returns routes, the real radar draws this route along the
 * river; without it the line would join the piers in a straight line across the land.
 */
const DEFAULT_ROUTE_CODE = 'RT01'

function WeatherBanners({ alerts }: { alerts: WeatherAlert[] }) {
  if (alerts.length === 0) return null
  return (
    <div className="mb-4 space-y-2">
      {sortAlerts(alerts).map((alert) => (
        <AlertBanner key={alert.id} alert={alert} />
      ))}
    </div>
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
            path={riverPathOf(routes.data[0]?.code ?? DEFAULT_ROUTE_CODE)}
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

export default function RadarPage() {
  useDocumentTitle('Live Operations')
  return <LiveRadar />
}
