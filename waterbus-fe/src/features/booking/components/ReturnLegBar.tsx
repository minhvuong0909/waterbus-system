import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppSelector } from '../../../store/hooks'
import { useSearchQuery } from '../hooks/useTripSearch'
import type { PierOption } from '../../trips/types'
import PierField from './PierField'
import { t } from '../../../i18n'

/**
 * Top of the return-trip step of a round trip: a reminder of the outbound choice, and the piers of
 * the return leg. They default to the way back, but the return may start at another pier
 * (BR-COM-03). The return day is picked in the date strip below.
 */
export default function ReturnLegBar({ piers }: { piers: PierOption[] }) {
  const { query, updateQuery } = useSearchQuery('return')
  const outbound = useAppSelector((s) => s.booking.selectedTrip)
  const outboundQuery = useAppSelector((s) => s.booking.query)
  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  // Picking the other field's pier swaps them, as on the main search form.
  const chooseOrigin = (id: string) =>
    updateQuery(id === query.destinationId ? { originId: id, destinationId: query.originId } : { originId: id })
  const chooseDestination = (id: string) =>
    updateQuery(id === query.originId ? { destinationId: id, originId: query.destinationId } : { destinationId: id })

  return (
    <div className="rounded-2xl border border-white/70 bg-white p-2.5 shadow-overlay">
      <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-mist px-3.5 py-2.5 text-sm">
        <span className="flex min-w-0 items-start gap-2 text-deep-river">
          <Icon name="check_circle" className="mt-px shrink-0 text-lg text-teal-flow" />
          <span className="shrink-0 whitespace-nowrap font-semibold">{t('Outbound')}:</span>
          <span className="min-w-0">
            {pier(outboundQuery.originId)?.shortName} → {pier(outboundQuery.destinationId)?.shortName}
            {outbound && ` · ${outbound.departTime}–${outbound.arriveTime}`}
          </span>
        </span>
        <Link to={ROUTES.searchResults} className="tap-area text-xs font-semibold text-teal-flow hover:underline">
          {t('Change outbound trip')}
        </Link>
      </div>
      <div className="relative mt-2 grid grid-cols-1 gap-2 md:grid-cols-2">
        <PierField
          className="min-w-0"
          label={t('Return from')}
          dotClass="bg-emerald-500 ring-4 ring-emerald-100"
          value={pier(query.originId)}
          options={piers}
          onChange={chooseOrigin}
          compact
        />
        <button
          type="button"
          aria-label={t('Swap origin and destination')}
          onClick={() => updateQuery({ originId: query.destinationId, destinationId: query.originId })}
          className="group absolute right-3 top-1/2 z-10 flex h-9 w-9 -translate-y-1/2 items-center justify-center rounded-full border border-outline-variant/50 bg-white text-deep-river shadow-sm transition-all before:absolute before:-inset-1 before:content-[''] hover:bg-teal-flow hover:text-on-primary md:left-1/2 md:right-auto md:-translate-x-1/2"
        >
          <Icon name="swap_horiz" className="rotate-90 text-lg md:rotate-0" />
        </button>
        <PierField
          className="min-w-0"
          label={t('Return to')}
          dotClass="bg-teal-flow ring-4 ring-teal-100"
          value={pier(query.destinationId)}
          options={piers}
          onChange={chooseDestination}
          compact
        />
      </div>
    </div>
  )
}
