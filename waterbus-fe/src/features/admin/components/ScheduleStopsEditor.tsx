import { CHECKBOX_CLASS, INPUT_CLASS } from '../../../components/ui/styles'
import { REQUIRED_LABEL_CLASS } from '../../../components/ui/styles'
import { t } from '../../../i18n'
import type { ScheduleStopRow } from '../utils/scheduleStops'

const OFFSET_CLASS = `${INPUT_CLASS} !min-h-[36px] !w-20 !px-2 text-right`

/**
 * Các bến lịch chạy phục vụ, theo thứ tự đi, kèm số phút sau giờ khởi hành (BR-STOP-01). Bến đầu rời đúng giờ khởi
 * hành, bến cuối chỉ có giờ đến.
 */
export default function ScheduleStopsEditor({
  rows,
  locked,
  onChange,
}: {
  rows: ScheduleStopRow[]
  locked: boolean
  onChange: (routeStopId: string, patch: Partial<ScheduleStopRow>) => void
}) {
  const served = rows.filter((row) => row.included)
  const first = served[0]?.routeStopId
  const last = served[served.length - 1]?.routeStopId

  return (
    <fieldset disabled={locked}>
      <legend className={`mb-1 text-xs font-semibold text-on-surface-variant ${REQUIRED_LABEL_CLASS}`}>
        {t('Stops and minutes after departure')}
      </legend>
      <div className="overflow-hidden rounded-xl border border-outline-variant/30">
        <div className="grid grid-cols-[1fr_5rem_5rem] gap-2 bg-mist px-3 py-2 text-2xs font-semibold text-on-surface-variant">
          <span>{t('Station')}</span>
          <span className="text-right">{t('Arrives')}</span>
          <span className="text-right">{t('Leaves')}</span>
        </div>
        {rows.map((row) => {
          const set = (key: 'arrival' | 'departure') => (e: { target: { value: string } }) =>
            onChange(row.routeStopId, { [key]: e.target.value })
          return (
            <div
              key={row.routeStopId}
              className="grid grid-cols-[1fr_5rem_5rem] items-center gap-2 border-t border-outline-variant/30 px-3 py-1.5"
            >
              <label className="flex min-w-0 items-center gap-2 text-sm text-deep-river">
                <input
                  type="checkbox"
                  className={CHECKBOX_CLASS}
                  checked={row.included}
                  onChange={(e) => onChange(row.routeStopId, { included: e.target.checked })}
                />
                <span className={`truncate ${row.included ? '' : 'text-on-surface-variant line-through'}`}>
                  {row.stationName}
                </span>
              </label>
              {row.included && row.routeStopId !== first ? (
                <input
                  aria-label={t('Minutes until arrival at {station}', { station: row.stationName })}
                  className={OFFSET_CLASS}
                  inputMode="numeric"
                  value={row.arrival}
                  onChange={set('arrival')}
                />
              ) : (
                <span className="text-right text-sm text-on-surface-variant">—</span>
              )}
              {row.included && row.routeStopId === first ? (
                <span className="text-right text-sm font-semibold text-deep-river">0</span>
              ) : row.included && row.routeStopId !== last ? (
                <input
                  aria-label={t('Minutes until departure from {station}', { station: row.stationName })}
                  className={OFFSET_CLASS}
                  inputMode="numeric"
                  value={row.departure}
                  onChange={set('departure')}
                />
              ) : (
                <span className="text-right text-sm text-on-surface-variant">—</span>
              )}
            </div>
          )
        })}
      </div>
    </fieldset>
  )
}
