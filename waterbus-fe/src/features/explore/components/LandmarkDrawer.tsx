import { useEffect, useRef } from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { Landmark } from '../types'

/** The landmark's photo, or a neutral tile while no photo of the right place is available. */
export function LandmarkImage({ landmark, className = '' }: { landmark: Landmark; className?: string }) {
  if (landmark.image) {
    return <img alt={landmark.title} src={landmark.image} loading="lazy" decoding="async" className={`object-cover ${className}`} />
  }
  return (
    <div
      aria-hidden="true"
      className={`flex items-center justify-center bg-gradient-to-br from-mist to-sand-light text-teal-flow/50 ${className}`}
    >
      <Icon name="photo_camera" className="text-[40px]" />
    </div>
  )
}

/**
 * Side panel with the story of a landmark. Esc, the close button or a click outside close it, Tab
 * stays inside it, and focus goes back to the card that opened it.
 */
export default function LandmarkDrawer({ landmark, onClose }: { landmark: Landmark; onClose: () => void }) {
  const closeRef = useRef<HTMLButtonElement>(null)
  // The parent passes a new function on every render; the effect below must run only once per opening,
  // or it would remember the wrong element to give focus back to.
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null
    closeRef.current?.focus()
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onCloseRef.current()
      // Keep focus inside the panel while it is open: its only control is the close button.
      if (e.key === 'Tab') {
        e.preventDefault()
        closeRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      window.removeEventListener('keydown', onKey)
      document.body.style.overflow = overflow
      opener?.focus?.()
    }
  }, [])

  return (
    <div className="fixed inset-0 z-[60] flex justify-end">
      <button
        type="button"
        aria-label={t('Close')}
        tabIndex={-1}
        onClick={onClose}
        className="absolute inset-0 bg-deep-river/40 backdrop-blur-[2px]"
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="landmark-drawer-title"
        className="relative flex h-full w-full max-w-md flex-col overflow-y-auto bg-white shadow-2xl"
      >
        <div className="relative h-56 shrink-0">
          <LandmarkImage landmark={landmark} className="h-full w-full" />
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            aria-label={t('Close')}
            className="absolute right-3 top-3 flex h-10 w-10 items-center justify-center rounded-full bg-white/90 text-deep-river shadow hover:bg-white"
          >
            <Icon name="close" className="text-[20px]" />
          </button>
        </div>
        <div className="space-y-space-sm p-space-lg">
          <span className="inline-block rounded-full bg-sand-light px-2.5 py-0.5 text-xs font-semibold text-teal-flow">
            {landmark.category}
          </span>
          <h2 id="landmark-drawer-title" className="font-headline-md text-headline-sm font-bold text-deep-river">
            {landmark.title}
          </h2>
          <p className="text-body-md leading-relaxed text-on-surface-variant">{landmark.description}</p>
          <p className="flex items-start gap-2 rounded-xl bg-mist px-space-md py-space-sm text-sm text-deep-river">
            <Icon name="campaign" className="mt-0.5 text-[18px] text-teal-flow" />
            {t('On sightseeing journeys, stories about the places you pass play over the boat’s speakers.')}
          </p>
        </div>
      </aside>
    </div>
  )
}
