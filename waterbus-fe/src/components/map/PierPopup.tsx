import type { ReactNode } from 'react'
import { pierPhoto } from '../../features/trips/pierImages'
import type { Pier } from '../../features/trips/types'
import { Icon } from '../ui'
import { t } from '../../i18n'

interface PierPopupProps {
  pier: Pier
  onClose: () => void
  /** Buttons under the details, e.g. "Depart from here". */
  actions?: ReactNode
}

/** Card with the photo and address of a pier, opened by clicking the pier on a map. */
export default function PierPopup({ pier, onClose, actions }: PierPopupProps) {
  const photo = pierPhoto(pier.code)
  const mapsUrl =
    pier.latitude !== undefined && pier.longitude !== undefined
      ? `https://www.google.com/maps/search/?api=1&query=${pier.latitude},${pier.longitude}`
      : undefined

  return (
    <div
      role="dialog"
      aria-label={pier.name}
      className="absolute bottom-4 right-4 z-30 w-72 max-w-[calc(100%-2rem)] overflow-hidden rounded-2xl border border-outline-variant/30 bg-white shadow-xl"
    >
      <div className="relative h-36 bg-gradient-to-br from-deep-river to-teal-flow">
        {photo ? (
          <img src={photo} alt={pier.name} className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-white/80">
            <Icon name="anchor" className="text-[36px]" />
            <span className="text-xs">{t('Photo coming soon')}</span>
          </div>
        )}
        <button
          type="button"
          onClick={onClose}
          aria-label={t('Close')}
          className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-white/90 text-deep-river shadow hover:bg-white"
        >
          <Icon name="close" className="text-[18px]" />
        </button>
      </div>
      <div className="space-y-space-xs p-space-md">
        <h3 className="font-headline-sm text-base font-bold text-deep-river">{pier.name}</h3>
        {pier.address && (
          <p className="flex items-start gap-1.5 text-xs text-on-surface-variant">
            <Icon name="location_on" className="text-[16px] text-teal-flow" />
            {pier.address}
          </p>
        )}
        {mapsUrl && (
          <a
            href={mapsUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1 text-xs font-semibold text-teal-flow hover:underline"
          >
            {t('Open in Google Maps')}
            <Icon name="open_in_new" className="text-[14px]" />
          </a>
        )}
        {actions && <div className="flex flex-wrap gap-2 pt-space-xs">{actions}</div>}
      </div>
    </div>
  )
}
