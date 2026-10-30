import { useState } from 'react'
import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { ExploreData, Landmark, PoiCategory } from '../types'
import { t } from '../../../i18n'
import LandmarkDrawer, { LandmarkImage } from './LandmarkDrawer'

const SECTION = 'mx-auto max-w-7xl px-margin'

/** Where both calls to action of the page lead: the departures, filtered to sightseeing trips. */
export const SIGHTSEEING_RESULTS = `${ROUTES.searchResults}?type=sightseeing`

const PRIMARY_CTA =
  'group inline-flex items-center gap-2 rounded-xl bg-teal-flow px-6 py-3 font-display text-sm font-semibold text-on-primary shadow-lg transition-colors hover:bg-secondary'

function SightseeingCta() {
  return (
    <Link to={SIGHTSEEING_RESULTS} className={PRIMARY_CTA}>
      {t('See sightseeing trips')}
      <Icon name="arrow_forward" className="text-lg transition-transform group-hover:translate-x-1" />
    </Link>
  )
}

export function ExploreHero() {
  return (
    <section className="relative overflow-hidden bg-deep-river text-on-primary">
      <img
        alt=""
        aria-hidden="true"
        src={vesselImage}
        className="absolute inset-0 h-full w-full object-cover opacity-25 mix-blend-luminosity"
      />
      <div className="absolute inset-0 bg-gradient-to-b from-deep-river/70 via-deep-river/85 to-deep-river" />
      <div className={`${SECTION} relative z-10 pb-space-3xl pt-space-3xl`}>
        <h1 className="mt-space-md max-w-2xl font-display text-4xl font-bold leading-tight tracking-tight md:text-headline-xl">
          {t('See the City Differently from the River')}
        </h1>
        <p className="mt-space-sm max-w-xl text-body-lg text-sand-light/90">
          {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
        </p>
        <div className="mt-space-lg">
          <SightseeingCta />
        </div>
      </div>
    </section>
  )
}

/**
 * Featured routes: editorial content that introduces the river, not trips to book. A card opens the
 * sightseeing departures. When every route is the same, it is said once under the heading.
 */
export function JourneysSection({ journeys }: { journeys: ExploreData['journeys'] }) {
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

export function LandmarksSection({
  categories,
  landmarks,
}: {
  categories: PoiCategory[]
  landmarks: ExploreData['landmarks']
}) {
  const [filter, setFilter] = useState<PoiCategory | 'all'>('all')
  const [open, setOpen] = useState<Landmark | null>(null)
  const visible = filter === 'all' ? landmarks : landmarks.filter((l) => l.category === filter)
  const pill = (active: boolean) =>
    `min-h-[36px] rounded-full px-4 py-1.5 text-sm transition-colors ${
      active ? 'bg-deep-river font-semibold text-white' : 'bg-white text-on-surface-variant hover:text-deep-river'
    }`

  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <div className="mb-space-lg flex flex-col justify-between gap-space-md md:flex-row md:items-end">
          <h2 className="font-display text-headline-lg tracking-tight text-deep-river">
            {t('Explore Along the River')}
          </h2>
          <div
            role="group"
            aria-label={t('Filter by category')}
            className="flex flex-wrap gap-1 rounded-full bg-surface-container p-1"
          >
            <button type="button" aria-pressed={filter === 'all'} onClick={() => setFilter('all')} className={pill(filter === 'all')}>
              {t('All')}
            </button>
            {categories.map((c) => (
              <button key={c} type="button" aria-pressed={filter === c} onClick={() => setFilter(c)} className={pill(filter === c)}>
                {c}
              </button>
            ))}
          </div>
        </div>
        {visible.length === 0 && (
          <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-lg text-center text-sm text-on-surface-variant">
            {t('No places in this category yet.')}
          </p>
        )}
        <div className="grid grid-cols-1 gap-space-md sm:grid-cols-2 lg:grid-cols-4">
          {visible.map((l) => (
            <button
              key={l.id}
              type="button"
              onClick={() => setOpen(l)}
              aria-haspopup="dialog"
              data-reveal
              className="group overflow-hidden rounded-2xl border border-outline-variant/30 bg-white text-left shadow-sm transition-all hover:shadow-lg focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow"
            >
              <div className="relative h-40 overflow-hidden">
                <LandmarkImage landmark={l} className="h-full w-full transition-transform duration-500 group-hover:scale-105" />
                <span className="absolute left-3 top-3 rounded-full bg-white/90 px-2.5 py-0.5 text-xs font-semibold text-deep-river">
                  {l.category}
                </span>
              </div>
              <div className="space-y-1 p-space-md">
                <h3 className="font-display text-lg font-bold text-deep-river">{l.title}</h3>
                <div className="flex items-center gap-1 pt-1 text-xs font-semibold text-teal-flow">
                  {t('Read the story')} <Icon name="arrow_forward" className="text-sm" />
                </div>
              </div>
            </button>
          ))}
        </div>
      </div>
      {open && <LandmarkDrawer landmark={open} onClose={() => setOpen(null)} />}
    </section>
  )
}

export function StoriesSection() {
  return (
    <section className="bg-deep-river py-space-2xl text-on-primary">
      <div className={`${SECTION} flex items-center gap-space-md`}>
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-teal-flow/25 text-sky-aqua">
          <Icon name="campaign" className="text-2xl" />
        </span>
        <div>
          <h2 className="font-display text-headline-sm font-bold">{t('Stories on board')}</h2>
          <p className="text-body-md text-sand-light/80">
            {t('On sightseeing journeys, stories about the places you pass play over the boat’s speakers.')}
          </p>
        </div>
      </div>
    </section>
  )
}

export function StepAboardBanner() {
  return (
    <section className="bg-surface py-space-xl">
      <div className={SECTION}>
        <div className="relative flex flex-col items-start justify-between gap-space-lg overflow-hidden rounded-panel bg-gradient-to-r from-deep-river to-teal-flow p-space-2xl text-on-primary md:flex-row md:items-center">
          <h2 className="max-w-xl font-display text-headline-lg tracking-tight">{t('Ready to come aboard?')}</h2>
          <SightseeingCta />
        </div>
      </div>
    </section>
  )
}
