import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { formatVnd } from '../../../utils/format'
import { ROUTES } from '../../../routes/routes'
import type { Departure } from '../types'
import { t } from '../../../i18n'

interface DepartureCardProps {
  departure: Departure
  onSelect: (tripId: string) => void
}

/** Compact departure row: time/status tile, route + details, price and a select action. */
export default function DepartureCard({ departure, onSelect }: DepartureCardProps) {
  const sightseeing = departure.kind === 'sightseeing'
  const statusClass = sightseeing
    ? 'text-coral-glow font-semibold'
    : departure.statusKind === 'scheduled'
    ? 'text-on-surface-variant'
    : 'text-teal-flow font-semibold'

  return (
    <div
      className={`group flex items-center justify-between gap-3 rounded-2xl border p-4 shadow-sm transition-all hover:shadow-md ${
        sightseeing
          ? 'border-coral-glow/30 bg-gradient-to-r from-surface to-mist'
          : 'border-[#E2E8F0] bg-surface hover:border-teal-flow/40'
      }`}
    >
      <div className="flex items-center gap-3.5">
        <div
          className={`flex h-12 w-12 flex-shrink-0 flex-col items-center justify-center rounded-xl border ${
            sightseeing
              ? 'border-coral-glow/30 bg-coral-glow/15 text-coral-glow'
              : 'border-outline-variant/30 bg-mist text-deep-river'
          }`}
        >
          <span className="font-headline-sm text-sm font-bold">{departure.time}</span>
          <span className={`text-[10px] ${statusClass}`}>{departure.status}</span>
        </div>
        <div>
          <div className="flex items-center gap-1.5 font-headline-sm text-sm font-bold text-deep-river">
            <span>{departure.from}</span>
            {departure.to && (
              <>
                <Icon name="arrow_forward" className="text-[15px] text-teal-flow" />
                <span>{departure.to}</span>
              </>
            )}
          </div>
          <div className="flex items-center gap-2 pt-0.5 text-xs text-on-surface-variant">
            {departure.vesselCode && (
              <>
                <span className="font-medium text-teal-flow">{departure.vesselCode}</span>
                <span>•</span>
              </>
            )}
            {departure.details.map((detail, i) => (
              <span key={detail} className="flex items-center gap-2">
                {i > 0 && <span>•</span>}
                <span
                  className={i === departure.details.length - 1 ? 'font-medium text-secondary' : ''}
                >
                  {detail}
                </span>
              </span>
            ))}
          </div>
        </div>
      </div>
      <div className="flex flex-col items-end text-right">
        <span className="font-numeric-md text-sm font-bold text-deep-river">
          {formatVnd(departure.priceVnd)}
        </span>
        <Link
          to={ROUTES.tripDetail}
          onClick={() => onSelect(departure.tripId)}
          className={`mt-1 rounded-full px-3.5 py-1 text-xs font-semibold text-on-primary transition-colors ${
            sightseeing ? 'bg-coral-glow hover:opacity-90' : 'bg-teal-flow hover:bg-secondary'
          }`}
        >
          {sightseeing ? t('Reserve') : t('Select')}
        </Link>
      </div>
    </div>
  )
}
