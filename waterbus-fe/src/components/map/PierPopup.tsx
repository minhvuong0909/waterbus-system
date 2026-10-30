import { useState } from 'react'
import type { ReactNode } from 'react'
import { pierPhotos } from '../../content/pierImages'
import type { Pier } from '../../features/trips/types'
import { Icon } from '../ui'
import { t } from '../../i18n'

interface PierPopupProps {
  pier: Pier
  onClose: () => void
  /** Buttons under the details, e.g. "Depart from here". */
  actions?: ReactNode
}

/**
 * Place panel for a pier, opened by clicking the pier on a map: a photo gallery on top, then the
 * name, address and the actions, much like a place card on a map app.
 */
export default function PierPopup({ pier, onClose, actions }: PierPopupProps) {
  const photos = pierPhotos(pier.code)
  const [index, setIndex] = useState(0)
  const current = Math.min(index, Math.max(photos.length - 1, 0))
  const step = (delta: number) => setIndex((current + delta + photos.length) % photos.length)

  return (
    <div
      role="dialog"
      aria-label={pier.name}
      className="absolute inset-x-3 bottom-3 top-auto z-30 max-h-[85%] overflow-y-auto rounded-2xl border border-outline-variant/30 bg-white shadow-xl sm:inset-x-auto sm:bottom-3 sm:left-3 sm:top-3 sm:max-h-none sm:w-80"
    >
      <div className="relative h-44 bg-gradient-to-br from-deep-river to-teal-flow">
        {photos.length > 0 ? (
          <img
            key={photos[current]}
            src={photos[current]}
            alt={t('{name}, photo {current} of {total}', {
              name: pier.name,
              current: current + 1,
              total: photos.length,
            })}
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="flex h-full flex-col items-center justify-center gap-1 text-white/80">
            <Icon name="photo_camera" className="text-4xl" />
            <span className="text-xs">{t('Photos coming soon')}</span>
          </div>
        )}

        {photos.length > 1 && (
          <>
            <button
              type="button"
              onClick={() => step(-1)}
              aria-label={t('Previous photo')}
              className="absolute left-2 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 text-deep-river shadow hover:bg-white"
            >
              <Icon name="chevron_left" className="text-xl" />
            </button>
            <button
              type="button"
              onClick={() => step(1)}
              aria-label={t('Next photo')}
              className="absolute right-2 top-1/2 flex h-8 w-8 -translate-y-1/2 items-center justify-center rounded-full bg-white/90 text-deep-river shadow hover:bg-white"
            >
              <Icon name="chevron_right" className="text-xl" />
            </button>
            <span className="absolute bottom-2 right-2 rounded-full bg-deep-river/75 px-2 py-0.5 text-2xs font-semibold text-white">
              {current + 1}/{photos.length}
            </span>
          </>
        )}

        <button
          type="button"
          onClick={onClose}
          aria-label={t('Close')}
          className="absolute right-2 top-2 flex h-8 w-8 items-center justify-center rounded-full bg-white/90 text-deep-river shadow hover:bg-white"
        >
          <Icon name="close" className="text-lg" />
        </button>
      </div>

      {photos.length > 1 && (
        <div className="flex gap-1.5 overflow-x-auto px-space-md pt-space-sm">
          {photos.map((url, i) => (
            <button
              key={url}
              type="button"
              onClick={() => setIndex(i)}
              aria-label={t('Photo {number}', { number: i + 1 })}
              aria-current={i === current}
              className={`h-12 w-16 shrink-0 overflow-hidden rounded-lg border-2 ${
                i === current ? 'border-teal-flow' : 'border-transparent opacity-70'
              }`}
            >
              <img src={url} alt="" className="h-full w-full object-cover" />
            </button>
          ))}
        </div>
      )}

      <div className="space-y-space-xs p-space-md">
        <h3 className="font-display text-lg font-bold text-deep-river">{pier.name}</h3>
        {pier.address && (
          <p className="flex items-start gap-1.5 text-sm text-on-surface-variant">
            <Icon name="location_on" className="text-lg text-teal-flow" />
            {pier.address}
          </p>
        )}
        {actions && <div className="flex flex-wrap gap-2 pt-space-xs">{actions}</div>}
      </div>
    </div>
  )
}
