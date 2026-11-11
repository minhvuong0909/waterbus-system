import { Link } from 'react-router-dom'
import { Icon, Panel } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import { useReload } from '../hooks/useAdmin'
import { getCurrentWeather, toKmh } from '../services/weatherService'
import type { RouteWeather, RouteWeatherStatus } from '../services/weatherService'
import { formatClock } from '../../../utils/time'

const STATUS: Record<RouteWeatherStatus, { label: string; icon: string; className: string }> = {
  Normal: { label: 'Normal', icon: 'check_circle', className: 'bg-teal-flow/10 text-teal-flow' },
  Caution: { label: 'Caution', icon: 'warning', className: 'bg-signal-amber/25 text-deep-river' },
  Danger: { label: 'Danger', icon: 'storm', className: 'bg-coral-glow/15 text-coral-glow' },
  Unknown: { label: 'No data', icon: 'help_outline', className: 'bg-surface-container text-on-surface-variant' },
}

const kmh = (ms: number | null) =>
  ms === null ? '—' : t('{speed} km/h', { speed: toKmh(ms).toLocaleString('vi-VN') })

function Measure({ icon, label, value }: { icon: string; label: string; value: string }) {
  return (
    <div className="flex min-w-0 items-center gap-2">
      <Icon name={icon} className="shrink-0 text-lg text-teal-flow" />
      <div className="min-w-0 leading-tight">
        <div className="text-2xs text-on-surface-variant">{t(label)}</div>
        <div className="truncate text-sm font-semibold text-deep-river">{value}</div>
      </div>
    </div>
  )
}

function RouteRow({ route }: { route: RouteWeather }) {
  const status = STATUS[route.status] ?? STATUS.Unknown
  return (
    <li className="grid grid-cols-1 gap-3 py-3 sm:grid-cols-[minmax(0,1.3fr)_auto_minmax(0,2fr)] sm:items-center sm:gap-4">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <span className="rounded-full bg-surface-container px-2 py-0.5 font-mono text-2xs font-semibold text-deep-river">
            {route.routeCode}
          </span>
          <span className="truncate text-sm font-semibold text-deep-river">{route.routeName}</span>
        </div>
        {route.pointsFailed > 0 && route.status !== 'Unknown' && (
          <p className="mt-1 text-2xs text-on-surface-variant">
            {t('Weather missing at {failed} of {total} piers.', { failed: route.pointsFailed, total: route.pointsChecked })}
          </p>
        )}
      </div>
      <span className={`inline-flex w-fit items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold ${status.className}`}>
        <Icon name={status.icon} className="text-base" />
        {t(status.label)}
      </span>
      <div className="grid grid-cols-3 gap-3">
        <Measure icon="air" label="Wind" value={kmh(route.windSpeedMs)} />
        <Measure icon="storm" label="Gusts" value={kmh(route.windGustMs)} />
        <Measure icon="cloud" label="Sky" value={route.description ?? '—'} />
      </div>
    </li>
  )
}

/**
 * Weather now on every active route, alert or not: wind, gusts, sky and a status from the same
 * thresholds as the weather check. Reading it never raises an alert or changes a trip.
 */
export default function CurrentWeatherPanel() {
  const [version, reload] = useReload()
  const weather = useFetch(getCurrentWeather, [version])
  const observedAt = weather.data
    ?.map((r) => r.observedAt)
    .filter((x): x is string => Boolean(x))
    .sort()
    .at(-1)

  return (
    <Panel className="mt-6">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold text-deep-river">{t('Current weather')}</h2>
          <p className="text-xs text-on-surface-variant">
            {observedAt
              ? t('Measured at {time} at the piers of each route.', { time: formatClock(new Date(observedAt)) })
              : t('Measured at the piers of each route.')}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Link
            to={ROUTES.adminIncidents}
            className="inline-flex min-h-[36px] items-center gap-1 rounded-full border border-outline-variant px-3 text-xs font-semibold text-deep-river hover:bg-surface-container-low"
          >
            {t('Weather alerts')}
          </Link>
          <button
            type="button"
            onClick={reload}
            disabled={weather.loading}
            className="inline-flex min-h-[36px] items-center gap-1 rounded-full bg-deep-river px-3 text-xs font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            <Icon name="refresh" className="text-base" />
            {weather.loading ? t('Updating…') : t('Refresh')}
          </button>
        </div>
      </div>

      {weather.error && !weather.data ? (
        <p role="alert" className="rounded-lg bg-coral-glow/10 px-3 py-2 text-sm text-deep-river">
          {t('Could not read the weather right now. Try again in a moment.')}
        </p>
      ) : !weather.data ? (
        <p className="text-sm text-on-surface-variant">{t('Loading weather…')}</p>
      ) : weather.data.length === 0 ? (
        <p className="text-sm text-on-surface-variant">{t('No active route to watch.')}</p>
      ) : (
        <ul className="divide-y divide-outline-variant/30">
          {weather.data.map((route) => (
            <RouteRow key={route.routeId} route={route} />
          ))}
        </ul>
      )}
    </Panel>
  )
}
