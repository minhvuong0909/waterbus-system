import { t } from '../../../i18n'

const SWATCH = 'h-4 w-4 rounded border'

/** The three looks of a seat on the map. Sold and held seats share one: neither can be chosen. */
export default function SeatLegend() {
  return (
    <ul className="flex flex-wrap items-center gap-x-space-md gap-y-1 text-xs text-on-surface-variant">
      <li className="flex items-center gap-1.5">
        <span className={`${SWATCH} border-outline-variant bg-white`} />
        {t('Available')}
      </li>
      <li className="flex items-center gap-1.5">
        <span className={`${SWATCH} border-teal-flow bg-teal-flow`} />
        {t('Selected')}
      </li>
      <li className="flex items-center gap-1.5">
        <span className={`${SWATCH} border-transparent bg-surface-container`} />
        {t('Not available')}
      </li>
    </ul>
  )
}
