import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { PierOption, SearchQuery } from '../types'
import { t } from '../../../i18n'

interface SearchSummaryBarProps {
  query: SearchQuery
  piers: PierOption[]
  dateLabel: string
}

function Item({
  icon,
  label,
  value,
  strong,
  iconClass = 'text-teal-flow',
}: {
  icon: string
  label: string
  value: string
  strong?: boolean
  iconClass?: string
}) {
  return (
    <div className="flex items-center gap-space-sm">
      <div
        className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-sand-light ${iconClass}`}
      >
        <Icon name={icon} className="text-[20px]" />
      </div>
      <div className="min-w-0">
        <span className="block text-[11px] font-semibold uppercase tracking-wider text-outline">
          {label}
        </span>
        <span
          className={`block truncate text-deep-river ${
            strong ? 'font-headline-sm text-sm font-bold md:text-base' : 'text-sm font-semibold'
          }`}
        >
          {value}
        </span>
      </div>
    </div>
  )
}

export default function SearchSummaryBar({ query, piers, dateLabel }: SearchSummaryBarProps) {
  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]

  return (
    <div className="flex flex-col items-stretch justify-between gap-space-md rounded-2xl border border-deep-river/10 bg-white p-space-md shadow-[0_4px_16px_rgba(13,37,56,0.04)] xl:flex-row xl:items-center">
      <div className="grid flex-1 grid-cols-2 items-center gap-space-md md:grid-cols-4">
        <Item
          icon="trip_origin"
          label={t('Origin Pier')}
          value={pier(query.originId).name}
          strong
        />
        <Item
          icon="location_on"
          label={t('Destination Pier')}
          value={pier(query.destinationId).name}
          strong
        />
        <Item
          icon="calendar_today"
          label={t('Departure Date')}
          value={dateLabel}
          iconClass="text-deep-river"
        />
        <Item
          icon="group"
          label={t('Passengers')}
          value={t(query.passengers)}
          iconClass="text-deep-river"
        />
      </div>
      <div className="flex shrink-0 items-center justify-end border-t border-surface-container pt-3 xl:border-l xl:border-t-0 xl:pl-space-md xl:pt-0">
        <Link
          to={ROUTES.search}
          className="inline-flex w-full items-center justify-center gap-space-xs rounded-full border border-teal-flow/15 bg-sand-light px-5 py-2.5 text-xs font-semibold text-deep-river transition-all hover:bg-[#dfe9e5] md:text-sm xl:w-auto"
        >
          <Icon name="tune" className="text-[18px] text-teal-flow" />
          {t('Modify Search')}
        </Link>
      </div>
    </div>
  )
}
