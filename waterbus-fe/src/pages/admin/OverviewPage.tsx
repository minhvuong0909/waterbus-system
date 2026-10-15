import { Link } from 'react-router-dom'
import { ErrorState, Icon, PageLoader } from '../../components/ui'
import { PageHeader, Panel, StatusBadge } from '../../features/admin/components/AdminUi'
import {
  getBoats,
  getRoutes,
  getStations,
  getTrips,
  getWeatherAlerts,
} from '../../features/admin/services/adminService'
import { formatDateTime, isFinished } from '../../features/admin/utils'
import { combineFetches, useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

function Kpi({ icon, label, value, to }: { icon: string; label: string; value: number; to: string }) {
  return (
    <Link
      to={to}
      className="flex items-center gap-4 rounded-xl border border-outline-variant/40 bg-white p-4 shadow-xs transition-colors hover:border-teal-flow/50"
    >
      <span className="flex h-11 w-11 items-center justify-center rounded-lg bg-teal-flow/10 text-teal-flow">
        <Icon name={icon} className="text-[24px]" />
      </span>
      <span>
        <span className="block text-2xl font-bold leading-none text-deep-river">{value}</span>
        <span className="text-xs font-semibold uppercase tracking-wide text-on-surface-variant">{t(label)}</span>
      </span>
    </Link>
  )
}

export default function OverviewPage() {
  useDocumentTitle('Overview')
  const trips = useFetch(getTrips)
  const boats = useFetch(getBoats)
  const stations = useFetch(getStations)
  const routes = useFetch(getRoutes)
  const alerts = useFetch(getWeatherAlerts)
  const state = combineFetches(trips, boats, stations, routes, alerts)

  if (state.error) return <ErrorState onRetry={state.retry} />
  if (state.loading || !trips.data || !boats.data || !stations.data || !routes.data || !alerts.data) return <PageLoader />

  const today = new Date().toDateString()
  const todays = trips.data.filter((x) => new Date(x.departureTime).toDateString() === today)
  const upcoming = trips.data.filter((x) => !isFinished(x.status)).slice(0, 5)
  const routeName = (id: string) => routes.data?.find((r) => r.id === id)?.name ?? '—'

  return (
    <>
      <PageHeader title="Overview" description="What is running today and what needs attention." />
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi icon="today" label="Trips today" value={todays.length} to={ROUTES.adminTrips} />
        <Kpi icon="directions_boat" label="On the river" value={trips.data.filter((x) => x.status === 'InTransit').length} to={ROUTES.adminRadar} />
        <Kpi icon="sailing" label="Boats" value={boats.data.length} to={ROUTES.adminBoats} />
        <Kpi icon="warning" label="Weather alerts" value={alerts.data.length} to={ROUTES.adminIncidents} />
      </div>
      <Panel title="Next trips" className="mt-6">
        {upcoming.length === 0 ? (
          <p className="text-sm text-on-surface-variant">{t('No trips are scheduled or running.')}</p>
        ) : (
          <ul className="divide-y divide-outline-variant/30">
            {upcoming.map((x) => (
              <li key={x.id} className="flex items-center justify-between gap-3 py-2 text-sm">
                <span className="font-semibold text-deep-river">{formatDateTime(x.departureTime)}</span>
                <span className="flex-1 truncate text-on-surface-variant">{routeName(x.routeId)}</span>
                <StatusBadge status={x.status} />
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </>
  )
}
