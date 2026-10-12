import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { PierOption, Trip } from '../types'
import { formatNumber } from '../../../utils/format'
import { seatsLabel } from '../utils'
import { t } from '../../../i18n'

interface TripCardProps {
  trip: Trip
  origin: PierOption
  destination: PierOption
  onSelect: (tripId: string) => void
}

const CARD: Record<Trip['variant'], string> = {
  featured: 'border-2 border-teal-flow/40 shadow-[0_4px_20px_rgba(20,122,126,0.08)]',
  default: 'border border-deep-river/10 shadow-xs',
  sunset: 'border-2 border-coral-glow/40 shadow-[0_4px_20px_rgba(240,138,107,0.10)]',
}

export default function TripCard({ trip, origin, destination, onSelect }: TripCardProps) {
  const sunset = trip.variant === 'sunset'
  const accent = sunset ? 'text-coral-glow' : 'text-teal-flow'

  return (
    <article
      className={`rounded-2xl bg-white p-space-lg transition-all duration-200 hover:shadow-md ${
        CARD[trip.variant]
      }`}
    >
      <div className="mb-space-md flex flex-wrap items-center justify-between gap-space-sm">
        <div className="flex flex-wrap items-center gap-space-xs">
          {trip.highlight && (
            <span
              className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-bold tracking-wide ${
                sunset
                  ? 'bg-coral-glow/15 text-coral-glow'
                  : 'bg-secondary-container text-on-secondary-container'
              }`}
            >
              <Icon name={trip.highlight.icon} className="text-[15px]" />
              {trip.highlight.label}
            </span>
          )}
          <span className="inline-flex items-center rounded-full bg-sand-light px-2.5 py-1 text-xs font-semibold text-deep-river">
            {trip.lineLabel}
          </span>
          {trip.tagline && (
            <span className="text-xs font-medium text-on-surface-variant">{trip.tagline}</span>
          )}
        </div>
        <div
          className={`flex items-center gap-1.5 text-xs font-bold ${
            trip.seatsLow ? 'text-coral-glow' : 'text-teal-flow'
          }`}
        >
          <span
            className={`h-2 w-2 rounded-full ${trip.seatsLow ? 'bg-coral-glow' : 'bg-teal-flow'}`}
          />
          {seatsLabel(trip)}
        </div>
      </div>

      <div className="grid grid-cols-1 items-center gap-space-md md:grid-cols-12">
        <div className="flex items-center gap-space-sm md:col-span-3 md:flex-col md:items-start">
          <div className="h-14 w-16 shrink-0 overflow-hidden rounded-lg border border-deep-river/10 bg-mist md:w-20">
            <img
              alt={`${trip.vesselCode} ${trip.vesselName}`}
              className="h-full w-full object-cover"
              src={vesselImage}
            />
          </div>
          <div>
            <span className="block font-headline-sm text-base font-bold text-deep-river">
              {trip.vesselCode}
            </span>
            <span className="text-xs font-medium text-on-surface-variant">{trip.vesselName}</span>
          </div>
        </div>

        <div className="flex items-center justify-between gap-space-sm py-1 md:col-span-6">
          <div className="text-left">
            <div className="font-headline-md text-xl font-bold text-deep-river md:text-2xl">
              {trip.departTime}
            </div>
            <div className="text-xs font-semibold text-deep-river">{origin.name}</div>
            <div className="text-[11px] text-on-surface-variant">{trip.originGate}</div>
          </div>
          <div className="flex flex-1 flex-col items-center px-2">
            <span className={`mb-1 text-center text-xs font-bold ${accent}`}>
              {t('{durationMins} min • {crossingKind}', {
                durationMins: trip.durationMins,
                crossingKind: trip.crossingKind,
              })}
            </span>
            <div className="relative flex w-full items-center">
              <div
                className={`h-2.5 w-2.5 shrink-0 rounded-full ${
                  sunset ? 'bg-coral-glow' : 'bg-teal-flow'
                }`}
              />
              <div
                className={`relative h-0.5 flex-1 ${
                  sunset
                    ? 'bg-gradient-to-r from-coral-glow via-signal-amber to-coral-glow'
                    : 'bg-gradient-to-r from-teal-flow via-sky-aqua to-teal-flow'
                }`}
              >
                <div
                  className={`absolute -top-2 left-1/2 flex h-4 w-4 -translate-x-1/2 items-center justify-center rounded-full border bg-white shadow-xs ${
                    sunset ? 'border-coral-glow text-coral-glow' : 'border-teal-flow text-teal-flow'
                  }`}
                >
                  <Icon name={sunset ? 'sailing' : 'directions_boat'} className="text-[10px]" />
                </div>
              </div>
              <div
                className={`h-2.5 w-2.5 shrink-0 rounded-full ${
                  sunset ? 'bg-coral-glow' : 'bg-teal-flow'
                }`}
              />
            </div>
            <span className="mt-1 text-center text-[11px] text-on-surface-variant">
              {trip.crossingCaption}
            </span>
          </div>
          <div className="text-right">
            <div className="font-headline-md text-xl font-bold text-deep-river md:text-2xl">
              {trip.arriveTime}
            </div>
            <div className="text-xs font-semibold text-deep-river">{destination.name}</div>
            <div className="text-[11px] text-on-surface-variant">{trip.destinationGate}</div>
          </div>
        </div>

        <div className="flex items-end justify-between gap-2 rounded-xl bg-sand-light/60 p-space-sm md:col-span-3 md:flex-col md:justify-center md:bg-transparent md:p-0 md:text-right">
          <div>
            <span className="block text-[11px] font-semibold uppercase tracking-wider text-outline">
              {trip.fareLabel}
            </span>
            <div className="font-headline-md text-xl font-bold text-deep-river">
              {formatNumber(trip.fareVnd)}{' '}
              <span className="text-xs font-medium text-on-surface-variant">{t('VND')}</span>
            </div>
          </div>
          <Link
            to={ROUTES.tripDetail}
            onClick={() => onSelect(trip.id)}
            className={`inline-flex items-center justify-center gap-1.5 rounded-full px-space-lg py-2 text-sm font-semibold transition-all ${
              sunset
                ? 'bg-coral-glow text-on-primary shadow-[0_2px_8px_rgba(240,138,107,0.3)] hover:opacity-90'
                : trip.variant === 'featured'
                ? 'bg-teal-flow text-on-primary shadow-[0_2px_8px_rgba(20,122,126,0.25)] hover:bg-secondary'
                : 'border border-teal-flow/15 bg-sand-light text-deep-river hover:bg-[#dfe9e5]'
            }`}
          >
            {t('Select Trip')}
            <Icon name="arrow_forward" className="text-[16px]" />
          </Link>
        </div>
      </div>

      <div className="mt-space-md flex flex-wrap items-center gap-space-md border-t border-deep-river/5 pt-space-sm text-xs text-on-surface-variant">
        {trip.amenities.map((a) => (
          <span key={a.label} className="inline-flex items-center gap-1">
            <Icon name={a.icon} className={`text-[15px] ${accent}`} />
            {a.label}
          </span>
        ))}
      </div>
    </article>
  )
}
