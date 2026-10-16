import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TrackingPoi, TrackingTrip } from '../types'
import { t } from '../../../i18n'

interface SidePanelProps {
  trip: TrackingTrip
  poi: TrackingPoi
}

/**
 * Right column: where the boat is and what it is passing. Narration is played over the boat's
 * speakers (BR-EXP-02), so there is no player here, only the text of the current story.
 */
export function SidePanel({ trip, poi }: SidePanelProps) {
  return (
    <aside className="space-y-space-md rounded-panel bg-white p-space-lg shadow-[0_4px_24px_rgba(13,37,56,0.06)]">
      <div>
        <h2 className="font-headline-md text-headline-md leading-tight text-deep-river">
          {poi.storyTitle}
        </h2>
        <p className="mt-1 text-sm text-on-surface-variant">{poi.storyText}</p>
        {poi.audio && (
          <p className="mt-space-sm flex items-center gap-1.5 text-xs text-teal-flow">
            <Icon name="campaign" className="text-[16px]" />
            {t('Playing over the boat’s speakers')}
          </p>
        )}
      </div>

      <div className="rounded-2xl border border-outline-variant/40 bg-mist/60 p-space-md">
        <div className="flex items-center justify-between text-sm">
          <span className="flex items-center gap-1.5 font-semibold text-deep-river">
            <Icon name="airline_seat_recline_extra" className="text-[18px] text-teal-flow" />
            {trip.seatLabel}
          </span>
          <span className="font-semibold text-teal-flow">
            {t('Est. Arrival {arrivalTime}', { arrivalTime: trip.arrivalTime })}
          </span>
        </div>
        <div className="mt-1 text-xs text-on-surface-variant">{trip.remainingLabel}</div>
        <Link
          to={ROUTES.ticketDetail}
          className="mt-space-sm flex items-center justify-center gap-1.5 rounded-lg bg-teal-flow py-2.5 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          <Icon name="qr_code" className="text-[18px]" />
          {t('View QR Ticket')}
        </Link>
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
      <h2 className="mb-space-md font-headline-lg text-headline-md text-deep-river">
        {t('Along Your River Journey')}
      </h2>

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
                  {t('Now')}
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
