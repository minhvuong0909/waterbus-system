import { Button, Icon } from '../../../components/ui'
import { INPUT_CLASS } from '../../../components/ui/styles'
import { t } from '../../../i18n'
import type { AdminStation, RouteStopInput } from '../types'

const iconButton =
  'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container hover:text-deep-river disabled:opacity-30 disabled:hover:bg-transparent'

/**
 * The stops of a route in calling order. Each stop says whether passengers may board and get off
 * there. `locked` shows them read only: a route used by a schedule or trip keeps its stops.
 */
export default function RouteStopsEditor({
  stops,
  stations,
  locked,
  onChange,
}: {
  stops: RouteStopInput[]
  stations: Pick<AdminStation, 'id' | 'name'>[]
  locked: boolean
  onChange: (stops: RouteStopInput[]) => void
}) {
  const update = (index: number, patch: Partial<RouteStopInput>) =>
    onChange(stops.map((stop, i) => (i === index ? { ...stop, ...patch } : stop)))

  const move = (index: number, by: -1 | 1) => {
    const next = [...stops]
    ;[next[index], next[index + by]] = [next[index + by], next[index]]
    onChange(next)
  }

  return (
    <fieldset disabled={locked}>
      <legend className="mb-1 text-xs font-semibold text-on-surface-variant">{t('Stops')}</legend>
      <ol className="space-y-2">
        {stops.map((stop, index) => (
          <li key={index} className="rounded-xl border border-outline-variant/40 p-2">
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
                  <option key={station.id} value={station.id}>
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
            <div className="mt-2 flex gap-4 pl-9 text-xs text-deep-river">
              <label className="flex items-center gap-1.5">
                <input
                  type="checkbox"
                  className="rounded border-outline-variant text-teal-flow focus:ring-teal-flow/30"
                  checked={stop.canBoard}
                  onChange={(e) => update(index, { canBoard: e.target.checked })}
                />
                {t('Passengers board')}
              </label>
              <label className="flex items-center gap-1.5">
                <input
                  type="checkbox"
                  className="rounded border-outline-variant text-teal-flow focus:ring-teal-flow/30"
                  checked={stop.canAlight}
                  onChange={(e) => update(index, { canAlight: e.target.checked })}
                />
                {t('Passengers get off')}
              </label>
            </div>
          </li>
        ))}
      </ol>
      {!locked && (
        <Button
          size="sm"
          variant="text"
          icon="add"
          className="mt-1"
          disabled={stops.length >= 50}
          onClick={() => onChange([...stops, { stationId: '', canBoard: true, canAlight: true }])}
        >
          {t('Add stop')}
        </Button>
      )}
    </fieldset>
  )
}
