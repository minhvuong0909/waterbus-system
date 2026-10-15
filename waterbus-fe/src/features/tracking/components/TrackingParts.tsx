import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import AudioPlayer from '../../../components/audio/AudioPlayer'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TrackingMode, TrackingPoi, TrackingTrip } from '../types'
import { t } from '../../../i18n'

export function ModeToggle({
  mode,
  onMode,
}: {
  mode: TrackingMode
  onMode: (m: TrackingMode) => void
}) {
  const tab = (active: boolean) =>
    `flex items-center gap-2 rounded-xl px-space-md py-2 text-sm font-semibold transition-all ${
      active
        ? 'bg-deep-river text-white shadow-sm'
        : 'text-on-surface-variant hover:text-deep-river'
    }`
  const link =
    'flex items-center gap-2 rounded-xl bg-white px-space-md py-2 text-sm font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low'

  return (
    <div className="flex flex-wrap items-center gap-space-sm">
      <div role="tablist" className="flex rounded-2xl bg-white p-1 shadow-sm">
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'sightseeing'}
          onClick={() => onMode('sightseeing')}
          className={tab(mode === 'sightseeing')}
        >
          <Icon name="headphones" className="text-[18px]" />
          {t('Sightseeing Experience')}
          {mode === 'sightseeing' && <span className="h-1.5 w-1.5 rounded-full bg-coral-glow" />}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === 'transit'}
          onClick={() => onMode('transit')}
          className={tab(mode === 'transit')}
        >
          <Icon name="commute" className="text-[18px]" />
          {t('Regular Transit')}
        </button>
      </div>
      <Link to={ROUTES.ticketDetail} className={link}>
        <Icon name="qr_code_2" className="text-[18px] text-teal-flow" />
        {t('View Ticket')}
      </Link>
      <Link to={ROUTES.tripDetail} className={`${link} bg-surface-container shadow-none`}>
        <Icon name="info" className="text-[18px] text-teal-flow" />
        {t('Trip Details')}
      </Link>
    </div>
  )
}

interface SidePanelProps {
  trip: TrackingTrip
  poi: TrackingPoi
  languages: string[]
  mode: TrackingMode
}

/** Right column: current story, audio player and trip/seat status. */
export function SidePanel({ trip, poi, languages, mode }: SidePanelProps) {
  const sightseeing = mode === 'sightseeing'

  return (
    <aside className="space-y-space-md rounded-panel bg-white p-space-lg shadow-[0_4px_24px_rgba(13,37,56,0.06)]">
      {sightseeing ? (
        <>
          <div className="flex items-center justify-between">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-coral-glow/15 px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-coral-glow">
              <Icon name="headphones" className="text-[14px]" />
              {poi.storyBadge}
            </span>
            <span className="flex items-end gap-0.5" aria-hidden="true">
              {[10, 16, 8, 14].map((h, i) => (
                <span key={i} className="w-1 rounded-full bg-teal-flow" style={{ height: h }} />
              ))}
            </span>
          </div>
          <h2 className="font-headline-md text-headline-md leading-tight text-deep-river">
            {poi.storyTitle}
          </h2>
          <p className="text-sm text-on-surface-variant">{poi.storyText}</p>

          <div className="relative overflow-hidden rounded-2xl">
            <img alt={poi.storyTitle} src={vesselImage} className="h-44 w-full object-cover" />
            <span className="absolute left-3 top-3 inline-flex items-center gap-1 rounded-full bg-deep-river/85 px-2.5 py-1 text-[11px] font-semibold text-white">
              <Icon name="volume_up" className="text-[14px]" />
              {t('River Story • {minutes} min', {
                minutes: poi.audio ? Math.round(poi.audio.durationSec / 60) : 0,
              })}
            </span>
            <div className="absolute inset-x-3 bottom-3 flex items-center justify-between text-[11px] font-semibold">
              <span className="text-sky-aqua">{poi.storyMeta}</span>
              <span className="rounded bg-deep-river/70 px-2 py-0.5 text-white">
                {t('GPS Auto-Triggered')}
              </span>
            </div>
          </div>

          {poi.audio ? (
            <AudioPlayer
              key={poi.audio.id}
              title={poi.audio.title}
              durationSec={poi.audio.durationSec}
              startAtSec={poi.audio.startAtSec}
              languages={languages}
            />
          ) : (
            <p className="rounded-2xl bg-mist/60 p-space-md text-sm text-on-surface-variant">
              {t(
                'No audio story at the terminal pier. Walking tour suggestions are ready after docking.',
              )}
            </p>
          )}
        </>
      ) : (
        <>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-on-secondary-container">
            <Icon name="commute" className="text-[14px]" />
            {t('Regular Transit')}
          </span>
          <h2 className="font-headline-md text-headline-md leading-tight text-deep-river">
            {t('Direct crossing in progress')}
          </h2>
          <p className="text-sm text-on-surface-variant">
            {t(
              'Sightseeing stories are switched off. Follow the live vessel position and the remaining track on the map.',
            )}
          </p>
        </>
      )}

      <div className="rounded-2xl border border-outline-variant/40 bg-mist/60 p-space-md">
        <div className="flex items-center justify-between text-xs">
          <span className="flex items-center gap-1.5 font-semibold text-deep-river">
            <Icon name="airline_seat_recline_extra" className="text-[16px] text-teal-flow" />
            {trip.seatLabel}
          </span>
          <span className="font-semibold text-teal-flow">
            {t('Est. Arrival {arrivalTime}', { arrivalTime: trip.arrivalTime })}
          </span>
        </div>
        <div className="mt-1 flex items-center justify-between text-xs text-on-surface-variant">
          <span>
            {t('Trip {code} • {vesselType}', { code: trip.code, vesselType: trip.vesselType })}
          </span>
          <span>{trip.remainingLabel}</span>
        </div>
        <div className="mt-space-sm grid grid-cols-2 gap-space-sm">
          <Link
            to={ROUTES.ticketDetail}
            className="flex items-center justify-center gap-1.5 rounded-lg bg-teal-flow py-2.5 text-xs font-semibold text-on-primary transition-colors hover:bg-secondary"
          >
            <Icon name="qr_code" className="text-[16px]" />
            {t('View QR Ticket')}
          </Link>
          <Link
            to={ROUTES.manageBooking}
            className="flex items-center justify-center gap-1.5 rounded-lg bg-white py-2.5 text-xs font-semibold text-deep-river transition-colors hover:bg-surface-container-low"
          >
            <Icon name="receipt_long" className="text-[16px]" />
            {t('Trip Receipt')}
          </Link>
        </div>
      </div>
    </aside>
  )
}

const ICON_BG: Record<TrackingPoi['state'], string> = {
  passed: 'bg-teal-flow text-white',
  active: 'bg-deep-river text-coral-glow',
  next: 'bg-secondary-container text-teal-flow',
  terminal: 'bg-mist text-outline',
}

export function Itinerary({
  pois,
  activePoiId,
  onSelect,
}: {
  pois: TrackingPoi[]
  activePoiId: string
  onSelect: (id: string) => void
}) {
  return (
    <section className="rounded-panel bg-white p-space-lg shadow-[0_4px_24px_rgba(13,37,56,0.06)] md:p-space-xl">
      <div className="mb-space-md flex flex-col justify-between gap-space-sm md:flex-row md:items-end">
        <div>
          <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
            {t('Spatial Itinerary')}
          </span>
          <h2 className="font-headline-lg text-headline-md text-deep-river">
            {t('Along Your River Journey')}
          </h2>
        </div>
        <span className="flex items-center gap-2 text-xs text-on-surface-variant">
          <Icon name="explore" className="text-[18px] text-coral-glow" />
          {t('Interactive Audio & Sightseeing POIs')}
        </span>
      </div>

      <div className="grid grid-cols-1 gap-space-md md:grid-cols-2 xl:grid-cols-4">
        {pois.map((poi) => {
          const active = poi.id === activePoiId
          return (
            <button
              key={poi.id}
              type="button"
              aria-pressed={active}
              onClick={() => onSelect(poi.id)}
              className={`relative rounded-2xl border p-space-md text-left transition-all ${
                active
                  ? 'border-2 border-teal-flow bg-white shadow-md'
                  : 'border-outline-variant/40 bg-mist/40 hover:bg-white hover:shadow-sm'
              }`}
            >
              {active && (
                <span className="absolute -top-3 right-4 rounded-full bg-coral-glow px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white">
                  {t('Listening Now')}
                </span>
              )}
              <div className="flex items-center justify-between">
                <span
                  className={`flex h-9 w-9 items-center justify-center rounded-full ${
                    ICON_BG[poi.state]
                  }`}
                >
                  <Icon
                    name={poi.state === 'passed' ? 'check' : poi.icon}
                    className="text-[20px]"
                  />
                </span>
                <span className="rounded bg-surface-container px-2 py-0.5 text-[11px] font-semibold text-on-surface-variant">
                  {poi.tag}
                </span>
              </div>
              <div className="mt-space-sm font-headline-sm text-base font-bold text-deep-river">
                {poi.title}
              </div>
              <p className="mt-1 text-xs text-on-surface-variant">{poi.description}</p>
              <div className="mt-space-sm flex items-center gap-1.5 text-xs font-semibold text-teal-flow">
                <Icon name={poi.audio ? 'headphones' : 'explore'} className="text-[14px]" />
                {poi.footer}
              </div>
            </button>
          )
        })}
      </div>
    </section>
  )
}
