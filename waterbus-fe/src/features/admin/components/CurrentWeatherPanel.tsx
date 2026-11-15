import { Icon, Notice, Panel } from '../../../components/ui'
import { t } from '../../../i18n'
import { useFetch, useReload } from '../../../hooks'
import { getCurrentWeather, toKmh } from '../services/weatherService'
import type { RouteWeather, RouteWeatherStatus } from '../services/weatherService'
import { formatClock } from '../../../utils/time'
import { useAutoRefresh } from '../hooks/useAutoRefresh'
import { useFleetHub } from '../../operations/hooks/useFleetHub'

const STATUS: Record<RouteWeatherStatus, { label: string; icon: string; className: string }> = {
  Normal: { label: 'Normal', icon: 'check_circle', className: 'bg-teal-flow/10 text-teal-flow' },
  Caution: { label: 'Warning', icon: 'warning', className: 'bg-signal-amber/25 text-deep-river' },
  Danger: { label: 'High risk', icon: 'storm', className: 'bg-coral-glow/15 text-coral-glow' },
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
 * Thời tiết hiện tại trên mọi tuyến đang hoạt động, có cảnh báo hay không: gió, gió giật, trời và trạng thái theo
 * cùng ngưỡng với lượt kiểm tra thời tiết. Việc đọc không tạo cảnh báo và không đổi chuyến nào.
 */
export default function CurrentWeatherPanel() {
  const [version, reload] = useReload()
  const weather = useFetch(getCurrentWeather, [version])
  useAutoRefresh(reload, weather.fetching)
  const connection = useFleetHub({ onAlert: reload, onReconnected: reload })
  const observedAt = weather.data
    ?.map((r) => r.observedAt)
    .filter((x): x is string => Boolean(x))
    .sort()
    .at(-1)

  return (
    <Panel
      title="Current weather"
      description={
        observedAt
          ? t('Measured at {time} at the piers of each route.', { time: formatClock(new Date(observedAt)) })
          : t('Measured at the piers of each route.')
      }
      actions={
        <span role="status" className="inline-flex items-center gap-2 text-xs font-semibold text-on-surface-variant">
          <span className={`h-2 w-2 rounded-full ${weather.error ? 'bg-coral-glow' : 'bg-teal-flow'}`} />
          {weather.error ? t('Weather update interrupted. Retrying automatically.') : weather.fetching ? t('Updating…') : t('Auto-updates every minute')}
        </span>
      }
    >
      {connection !== 'live' && <p className="mb-3 text-xs text-on-surface-variant">{t('Reconnecting to live alerts. Periodic updates remain active.')}</p>}

      {weather.error && !weather.data ? (
        <Notice tone="error">{t('Could not read the weather right now. Try again in a moment.')}</Notice>
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
