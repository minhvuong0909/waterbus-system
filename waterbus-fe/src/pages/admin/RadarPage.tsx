import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import FleetMap from '../../features/tracking/components/FleetMap'
import { ErrorState, Icon, PageHeader, PageLoader, Panel } from '../../components/ui'
import AlertBanner from '../../features/admin/components/AlertBanner'
import { alertTone, pendingCountOf, sortAlerts } from '../../features/admin/utils/alertUtils'
import { fleetRows } from '../../features/operations/fleetRows'
import type { FleetRow } from '../../features/operations/fleetRows'
import { useFleetHub, useLiveFleet } from '../../features/operations/hooks/useFleetHub'
import { getBoats } from '../../features/operations/services/fleetService'
import {
  getOpenIncidents,
  getOperationTrips,
  knotsToKmh,
} from '../../features/operations/services/operationsService'
import { useAutoRefresh } from '../../features/admin/hooks/useAutoRefresh'
import { getStations } from '../../features/admin/services/stationService'
import type { IncidentAlert } from '../../features/operations/types'
import { riverPathOf } from '../../constant/riverPaths'
import { SIGHTSEEING_POIS } from '../../constant/sightseeingPois'
import { combineFetches, useDocumentTitle, useFetch, useNow, useReload } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import type { RealtimeStatus } from '../../services/realtime'
import { formatClockWithSeconds, formatDateTime } from '../../utils/time'

/**
 * Đường đi dọc sông chỉ là dữ liệu vẽ bản đồ ở FE (`constant/riverPaths.ts`), hiện có cho tuyến RT01; BE chưa có mô
 * hình đường đi của tuyến. Tuyến RT02 là chiều về của cùng hành lang nên dùng chung đường này.
 */
const ROUTE_CODE = 'RT01'

/** Một cảnh báo: dải chi tiết. Nhiều cảnh báo: gộp thành một dải để không đẩy bản đồ ra khỏi màn hình. */
function IncidentBanners({ alerts }: { alerts: IncidentAlert[] }) {
  if (alerts.length === 0) return null
  const sorted = sortAlerts(alerts)
  if (sorted.length === 1) {
    return (
      <div className="mb-4">
        <AlertBanner alert={sorted[0]} />
      </div>
    )
  }
  const tone = alertTone(sorted[0])
  const pending = alerts.reduce((sum, alert) => sum + pendingCountOf(alert), 0)
  return (
    <div
      role="alert"
      className={`mb-4 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-l-4 border-outline-variant/30 bg-white px-4 py-3 text-sm shadow-sm ${tone.accent}`}
    >
      <span className={`flex h-8 w-8 items-center justify-center rounded-xl ${tone.badge}`}>
        <Icon name={tone.icon} className="text-lg" />
      </span>
      <div className="min-w-0 flex-1 font-semibold text-deep-river">
        {t('{count} open alerts', { count: alerts.length })}
        {pending > 0 && (
          <span className="ml-2 rounded-full bg-coral-glow/15 px-2.5 py-1 text-xs text-coral-glow">
            {t('{count} trips need a decision', { count: pending })}
          </span>
        )}
      </div>
      <Link
        to={ROUTES.adminIncidents}
        className="inline-flex min-h-[36px] items-center gap-1 rounded-full bg-deep-river px-3 text-xs font-semibold text-white hover:opacity-90"
      >
        {t('Review alerts')}
        <Icon name="arrow_forward" className="text-base" />
      </Link>
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

const SIGNAL_TEXT: Record<FleetRow['signal'], { label: string; className: string }> = {
  live: { label: 'Live', className: 'text-teal-flow' },
  stale: { label: 'No signal', className: 'text-signal-amber' },
  none: { label: 'No position yet', className: 'text-coral-glow' },
}

function BoatRow({ row }: { row: FleetRow }) {
  const signal = SIGNAL_TEXT[row.signal]
  const speed = knotsToKmh(row.position?.speedKnots)
  return (
    <li className="rounded-xl border border-outline-variant/30 p-3">
      <div className="flex items-center justify-between">
        <span className="font-mono text-sm font-bold text-deep-river">{row.code}</span>
        <span className={`text-xs font-semibold ${signal.className}`}>{t(signal.label)}</span>
      </div>
      {row.trip ? (
        <Link
          to={ROUTES.adminTrips}
          className="mt-2 block truncate text-sm text-deep-river transition-colors hover:text-teal-flow"
        >
          {formatDateTime(row.trip.departureTime)} · {row.trip.routeName}
        </Link>
      ) : (
        <p className="mt-2 text-sm text-on-surface-variant">{t('Not on a trip')}</p>
      )}
      {row.position && (
        <div className="mt-1 flex justify-between text-xs text-on-surface-variant">
          <span>{t('Updated {time}', { time: formatClockWithSeconds(row.position.observedAt) })}</span>
          {speed != null && <span>{t('{speed} km/h', { speed })}</span>}
        </div>
      )}
    </li>
  )
}

/** Radar: tàu đang phục vụ (chuyến từ API) ghép với vị trí GPS / bộ giả lập qua hub theo dõi tàu. */
export default function RadarPage() {
  useDocumentTitle('Live Operations')
  const [alertVersion, reloadAlerts] = useReload()
  const [tripVersion, reloadTrips] = useReload()
  const stations = useFetch(getStations)
  const boats = useFetch(getBoats)
  const trips = useFetch(getOperationTrips, [tripVersion])
  const alerts = useFetch(() => getOpenIncidents(), [alertVersion])
  // Báo khẩn cấp (incidentRaised), đổi trạng thái chuyến và kết nối lại đều tải lại danh sách sự cố qua REST;
  // useAutoRefresh bên dưới là đường dự phòng có chặn nếu lỡ một lần đẩy.
  const fleet = useLiveFleet({ onIncident: reloadAlerts, onTripStatus: reloadAlerts, onReconnected: reloadAlerts })
  // Chuyến đổi trạng thái (đón khách, xuất bến, tạm ngưng…) thì danh sách tàu đang phục vụ đổi theo.
  useFleetHub({ onTripStatus: reloadTrips, onReconnected: reloadTrips })
  useAutoRefresh(() => { reloadAlerts(); reloadTrips() }, alerts.fetching || trips.fetching)
  const now = useNow(15_000) // tính lại nhãn "mất tín hiệu"
  const state = combineFetches(stations, boats, trips, alerts)

  const rows = useMemo(
    () =>
      fleetRows(
        trips.data ?? [],
        fleet.positions,
        new Map((boats.data ?? []).map((b) => [b.id, b.code])),
        now,
      ),
    [trips.data, fleet.positions, boats.data, now],
  )

  if (state.error || fleet.error) return <ErrorState onRetry={state.error ? state.retry : fleet.retry} />
  if (state.loading || !fleet.loaded || !stations.data || !boats.data || !trips.data || !alerts.data) {
    return <PageLoader />
  }

  const corridor = stations.data.filter((s) => s.isActive).sort((a, b) => a.orderIndex - b.orderIndex)
  // Chỉ vẽ tàu đang trong chuyến: tàu nằm chờ ở bến không cần chiếm chỗ trên bản đồ.
  const mapBoats = rows.flatMap((row) =>
    row.position && row.trip
      ? [
          {
            tripId: row.boatId,
            code: row.code,
            latitude: row.position.latitude,
            longitude: row.position.longitude,
            stale: row.signal === 'stale',
          },
        ]
      : [],
  )
  const lost = rows.filter((row) => row.signal !== 'live').length

  return (
    <>
      <PageHeader title="Live Operations" description="Boats on the Saigon River, updated as their GPS reports arrive." />
      <IncidentBanners alerts={alerts.data} />
      <div className="grid gap-4 lg:grid-cols-3">
        <div className="h-[78vh] min-h-[24rem] lg:col-span-2">
          <FleetMap stations={corridor} path={riverPathOf(ROUTE_CODE)} landmarks={SIGHTSEEING_POIS} boats={mapBoats} />
        </div>
        <Panel title="Boats in service">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <LiveIndicator status={fleet.status} />
            {lost > 0 && (
              <span className="text-xs font-semibold text-signal-amber">
                {t('{count} without a signal', { count: lost })}
              </span>
            )}
          </div>
          {rows.length === 0 ? (
            <p className="text-sm text-on-surface-variant">{t('No boat is on a trip right now.')}</p>
          ) : (
            <ul className="space-y-3">
              {rows.map((row) => (
                <BoatRow key={row.boatId} row={row} />
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </>
  )
}
