import FleetMap from '../../components/map/FleetMap'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { PageHeader, Panel, StatusBadge } from '../../features/admin/components/AdminUi'
import { useTicker } from '../../features/admin/hooks/useAdmin'
import {
  getBoats,
  getFleetPositions,
  getRoutes,
  getStations,
  getTrips,
  getWeatherAlerts,
} from '../../features/admin/services/adminService'
import { formatClockTime, routeStations } from '../../features/admin/utils'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { t } from '../../i18n'

/** How often the boat positions are refreshed (the roadmap asks for updates in under 3 seconds). */
const REFRESH_MS = 3000

export default function RadarPage() {
  useDocumentTitle('Live Operations')
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
      {alerts.data.map((alert) => (
        <div
          key={alert.id}
          role="alert"
          className="mb-4 flex items-center gap-3 rounded-xl border border-signal-amber/50 bg-signal-amber/15 px-4 py-3 text-sm text-deep-river"
        >
          <Icon name="air" className="text-[20px] text-signal-amber" />
          <span className="font-semibold">
            {t('Weather warning: wind {speed} km/h', { speed: alert.windSpeedKmh })}
          </span>
          <span className="text-on-surface-variant">{t(alert.message)}</span>
        </div>
      ))}
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
