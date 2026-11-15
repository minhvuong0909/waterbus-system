import { Button, Icon } from '../../../components/ui'
import { INPUT_CLASS } from '../../../components/ui/styles'
import { REQUIRED_LABEL_CLASS } from '../../../components/ui/styles'
import { t } from '../../../i18n'
import type { AdminStation, RouteStopInput, ServiceType } from '../types'

const iconButton =
  'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-on-surface-variant transition-colors hover:bg-surface-container hover:text-deep-river disabled:opacity-30 disabled:hover:bg-transparent'

/**
 * Các bến của tuyến theo thứ tự ghé. Khách lên hoặc xuống ở bất kỳ bến nào (vé một giá theo loại chuyến). `locked` hiện chỉ đọc:
 * tuyến đã được lịch chạy hoặc chuyến dùng giữ nguyên bến dừng.
 */
export default function RouteStopsEditor({
  stops,
  stations,
  serviceType,
  locked,
  onChange,
}: {
  stops: RouteStopInput[]
  stations: Pick<AdminStation, 'id' | 'name'>[]
  /** Tuyến Thường mỗi bến chỉ ghé một lần; tuyến ngắm cảnh được đi vòng nên được chọn lại bến. */
  serviceType: ServiceType
  locked: boolean
  onChange: (stops: RouteStopInput[]) => void
}) {
  const eachStationOnce = serviceType === 'Regular'
  const allStationsUsed = eachStationOnce && stops.length >= stations.length
  const update = (index: number, patch: Partial<RouteStopInput>) =>
    onChange(stops.map((stop, i) => (i === index ? { ...stop, ...patch } : stop)))

  const move = (index: number, by: -1 | 1) => {
    const next = [...stops]
    ;[next[index], next[index + by]] = [next[index + by], next[index]]
    onChange(next)
  }

  return (
    <fieldset disabled={locked}>
      <legend className={`mb-1 text-xs font-semibold text-on-surface-variant ${REQUIRED_LABEL_CLASS}`}>{t('Stops')}
      </legend>
      <ol className="space-y-2">
        {stops.map((stop, index) => (
          <li key={index} className="rounded-xl border border-outline-variant/30 p-2">
            <div className="flex items-center gap-2">
              <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-mist text-xs font-bold text-deep-river">
                {index + 1}
              </span>
              <select
                aria-label={t('Station of stop {number}', { number: index + 1 })}
                className={INPUT_CLASS}
                value={stop.stationId}
                onChange={(e) => update(index, { stationId: e.target.value })}
              >
                <option value="">{t('Choose a station')}</option>
                {stations.map((station) => (
                  <option
                    key={station.id}
                    value={station.id}
                    disabled={eachStationOnce && stops.some((other, i) => i !== index && other.stationId === station.id)}
                  >
                    {station.name}
                  </option>
                ))}
              </select>
              {!locked && (
                <>
                  <button type="button" className={iconButton} aria-label={t('Move up')} disabled={index === 0} onClick={() => move(index, -1)}>
                    <Icon name="arrow_downward" className="rotate-180 text-lg" />
                  </button>
                  <button type="button" className={iconButton} aria-label={t('Move down')} disabled={index === stops.length - 1} onClick={() => move(index, 1)}>
                    <Icon name="arrow_downward" className="text-lg" />
                  </button>
                  <button type="button" className={iconButton} aria-label={t('Remove stop')} disabled={stops.length <= 2} onClick={() => onChange(stops.filter((_, i) => i !== index))}>
                    <Icon name="close" className="text-lg" />
                  </button>
                </>
              )}
            </div>
          </li>
        ))}
      </ol>
      {!locked && allStationsUsed && (
        <p className="mt-1 text-2xs text-on-surface-variant">
          {t('Every station is already on this route. A regular route calls at each station once.')}
        </p>
      )}
      {!locked && (
        <Button
          size="sm"
          variant="text"
          icon="add"
          className="mt-1"
          disabled={stops.length >= 50 || allStationsUsed}
          onClick={() => onChange([...stops, { stationId: '', canBoard: true, canAlight: true }])}
        >
          {t('Add stop')}
        </Button>
      )}
    </fieldset>
  )
}
