import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import type { ExploreData } from '../types'
import { t } from '../../../i18n'
import { SECTION, SIGHTSEEING_RESULTS } from '../constants'

/**
 * Featured routes: editorial content that introduces the river, not trips to book. A card opens the
 * sightseeing departures. When every route is the same, it is said once under the heading.
 */
export default function JourneysSection({ journeys }: { journeys: ExploreData['journeys'] }) {
  const sharedRoute = journeys.length > 0 && journeys.every((j) => j.route === journeys[0].route) ? journeys[0].route : null

  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <div className="mb-space-lg">
          <h2 className="font-display text-headline-lg tracking-tight text-deep-river">{t('Featured Routes')}</h2>
          {sharedRoute && (
            <p className="mt-1 flex items-center gap-1.5 text-sm text-on-surface-variant">
              <Icon name="near_me" className="text-base text-teal-flow" />
              {t('All run {route}', { route: sharedRoute })}
            </p>
          )}
        </div>
        <div className="grid grid-cols-1 gap-space-lg md:grid-cols-3">
          {journeys.map((j) => (
            <Link
              key={j.id}
              to={SIGHTSEEING_RESULTS}
              data-reveal
              className="group flex flex-col overflow-hidden rounded-panel border border-outline-variant/30 bg-white shadow-sm transition-all hover:shadow-xl"
            >
              <div className="relative h-52 overflow-hidden">
                <img
                  alt={j.title}
                  src={j.image}
                  loading="lazy"
                  decoding="async"
                  className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
                />
              </div>
              <div className="flex flex-1 flex-col gap-space-sm p-space-lg">
                <div className="flex flex-wrap gap-2">
                  {j.tags.map((tag) => (
                    <span
                      key={tag}
                      className="whitespace-nowrap rounded-full bg-sand-light px-2.5 py-0.5 text-xs font-semibold text-teal-flow"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
                <h3 className="flex items-start justify-between gap-2 font-display text-xl font-bold text-deep-river">
                  {j.title}
                  <Icon
                    name="arrow_forward"
                    className="mt-1 shrink-0 text-xl text-teal-flow transition-transform group-hover:translate-x-1"
                  />
                </h3>
                {!sharedRoute && (
                  <div className="flex items-center gap-1.5 text-xs text-on-surface-variant">
                    <Icon name="near_me" className="text-sm text-teal-flow" />
                    {j.route}
                  </div>
                )}
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  )
}
