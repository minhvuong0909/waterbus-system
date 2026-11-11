import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

export default function SeatLegend() {
  return (
    <div className="flex flex-wrap items-center gap-space-md text-xs text-on-surface-variant">
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded border-2 border-teal-flow/40 bg-white text-3xs font-bold text-deep-river">
          {t('A1')}
        </span>
        {t('Available')}
      </span>
      <span className="flex items-center gap-1.5 font-semibold text-deep-river">
        <span className="flex h-5 w-5 items-center justify-center rounded bg-teal-flow text-3xs font-bold text-white">
          {t('A2')}
        </span>
        {t('Selected')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded border-2 border-dashed border-signal-amber bg-signal-amber/15 text-deep-river/70">
          <Icon name="hourglass_top" className="text-xs" />
        </span>
        {t('Held by another traveler')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded bg-outline-variant/50 text-outline">
          <Icon name="block" className="text-xs" />
        </span>
        {t('Sold')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex h-5 w-5 items-center justify-center rounded-full border border-sky-aqua bg-sky-aqua/20">
          <span className="h-2 w-2 rounded-full bg-sky-aqua" />
        </span>
        {t('Window River View')}
      </span>
    </div>
  )
}
