import { Link } from 'react-router-dom'
import { ErrorState, Icon, PageHeader, PageLoader, Panel } from '../../components/ui'
import { useReload } from '../../features/admin/hooks/useAdmin'
import { useFleetHub } from '../../features/admin/hooks/useFleetHub'
import { getBoats } from '../../features/admin/services/boatService'
import { getOpenIncidents, getOperationTrips } from '../../features/admin/services/operationsService'
import { isFinished } from '../../features/admin/utils'
import { formatDateTime } from '../../utils/time'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import OverviewMotion from '../../features/admin/components/OverviewMotion'
import CurrentWeatherPanel from '../../features/admin/components/CurrentWeatherPanel'
import TripStatusBadge from '../../features/trips/components/TripStatusBadge'

function Kpi({
  icon,
  label,
  value,
  to,
}: {
  icon: string
  label: string
  value: number
  to: string
}) {
  return (
    <Link
      to={to}
      data-admin-enter
      className="hover-lift flex min-w-0 items-center gap-3 rounded-2xl border border-outline-variant/30 bg-white p-4 shadow-card hover:border-teal-flow/40 sm:gap-4 sm:p-5"
    >
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-teal-flow/10 text-teal-flow sm:h-12 sm:w-12 sm:rounded-2xl">
        <Icon name={icon} className="text-2xl" />
      </span>
      <span className="min-w-0">
        <span className="mb-1 block text-3xl font-bold leading-none tabular-nums tracking-tight text-deep-river">
          {value}
        </span>
        <span className="text-xs font-medium text-on-surface-variant">{t(label)}</span>
      </span>
    </Link>
  )
}

export default function OverviewPage() {
  useDocumentTitle('Overview')
  const [version, reload] = useReload()
  const trips = useFetch(getOperationTrips, [version])
  const boats = useFetch(getBoats)
  const alerts = useFetch(() => getOpenIncidents(), [version])
  // Counts and the next trips follow status changes and new alerts as they happen.
  useFleetHub(
    { onTripStatus: reload, onAlert: reload, onIncident: reload, onReconnected: reload },
  )
  const state = combineFetches(trips, boats, alerts)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !boats.data || !alerts.data) return <PageLoader />

  const today = new Date().toDateString()
  const todays = trips.data.filter((x) => new Date(x.departureTime).toDateString() === today)
  const upcoming = trips.data.filter((x) => !isFinished(x.status)).slice(0, 5)

  return (
    <OverviewMotion>
      <PageHeader title="Overview" description="What is running today and what needs attention." />
      <div className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        <Kpi icon="today" label="Trips today" value={todays.length} to={ROUTES.adminTrips} />
        <Kpi
          icon="directions_boat"
          label="On the river"
          value={trips.data.filter((x) => x.status === 'EnRoute').length}
          to={ROUTES.adminRadar}
        />
        <Kpi icon="sailing" label="Boats" value={boats.data.length} to={ROUTES.adminBoats} />
        <Kpi
          icon="warning"
          label="Open alerts"
          value={alerts.data.length}
          to={ROUTES.adminIncidents}
        />
      </div>
      <CurrentWeatherPanel />
      <Panel title="Next trips" className="mt-6">
        {upcoming.length === 0 ? (
          <p className="text-sm text-on-surface-variant">
            {t('No trips are scheduled or running.')}
          </p>
        ) : (
          <ul className="divide-y divide-outline-variant/30">
            {upcoming.map((x) => (
              <li
                key={x.id}
                className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 py-3 text-sm"
              >
                <span className="font-semibold text-deep-river">
                  {formatDateTime(x.departureTime)}
                </span>
                <span className="flex-1 truncate text-on-surface-variant">
                  {x.routeName}
                </span>
                <TripStatusBadge status={x.status} />
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </OverviewMotion>
  )
}
