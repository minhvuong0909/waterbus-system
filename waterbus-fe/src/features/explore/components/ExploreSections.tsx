import { useState } from 'react'
import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { ExploreData, PoiCategory } from '../types'
import { t } from '../../../i18n'

const SECTION = 'mx-auto max-w-7xl px-margin'

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
        <h1 className="mt-space-md max-w-2xl font-headline-xl text-4xl font-bold leading-tight tracking-tight md:text-headline-xl">
          {t('See the City Differently from the River')}
        </h1>
        <div className="mt-space-lg flex flex-wrap gap-space-md">
          <Link
            to={ROUTES.searchResults}
            className="group inline-flex items-center gap-2 rounded-xl bg-teal-flow px-6 py-3 font-headline-sm text-sm font-semibold text-on-primary shadow-lg transition-colors hover:bg-secondary"
          >
            {t('Explore Sightseeing Journeys')}
            <Icon
              name="arrow_forward"
              className="text-[18px] transition-transform group-hover:translate-x-1"
            />
          </Link>
        </div>
      </div>
    </section>
  )
}

export function JourneysSection({ journeys }: { journeys: ExploreData['journeys'] }) {
  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <div className="mb-space-lg flex flex-col justify-between gap-space-sm md:flex-row md:items-end">
          <div>
            <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Sightseeing Journeys')}
            </h2>
          </div>
          <Link
            to={ROUTES.searchResults}
            className="tap-area flex items-center gap-1 text-sm font-semibold text-teal-flow hover:underline"
          >
            {t('Browse All Journeys')} <Icon name="arrow_forward" className="text-[16px]" />
          </Link>
        </div>
        <div className="grid grid-cols-1 gap-space-lg md:grid-cols-3">
          {journeys.map((j) => (
            <Link
              key={j.id}
              to={ROUTES.searchResults}
              data-reveal
              className="group overflow-hidden rounded-panel border border-outline-variant/30 bg-white shadow-sm transition-all hover:shadow-xl"
            >
              <div className="relative h-52 overflow-hidden">
                <img
                  alt={j.title}
                  src={j.image}
                  className="h-full w-full object-cover transition-transform duration-700 group-hover:scale-105"
                />
                <span className="absolute left-3 top-3 rounded-full bg-deep-river/85 px-3 py-1 text-[11px] font-semibold text-white backdrop-blur">
                  {j.eyebrow}
                </span>
              </div>
              <div className="space-y-space-sm p-space-lg">
                <div className="flex gap-2">
                  {j.tags.map((tag) => (
                    <span
                      key={tag}
                      className="rounded-full bg-sand-light px-2.5 py-0.5 text-[11px] font-semibold text-teal-flow"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
                <h3 className="font-headline-sm text-xl font-bold text-deep-river">{j.title}</h3>
                <div className="flex items-center gap-1.5 text-xs text-on-surface-variant">
                  <Icon name="near_me" className="text-[15px] text-teal-flow" />
                  {j.route}
                </div>
                <div className="flex items-center justify-between border-t border-surface-container pt-space-sm text-sm font-semibold text-teal-flow">
                  {t('Explore Journey')}
                  <Icon
                    name="arrow_forward"
                    className="text-[18px] transition-transform group-hover:translate-x-1"
                  />
                </div>
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
  const visible = filter === 'all' ? landmarks : landmarks.filter((l) => l.category === filter)
  const pill = (active: boolean) =>
    `rounded-full px-4 py-1.5 text-sm transition-colors ${
      active
        ? 'bg-deep-river font-semibold text-white'
        : 'bg-white text-on-surface-variant hover:text-deep-river'
    }`

  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <div className="mb-space-lg flex flex-col justify-between gap-space-md md:flex-row md:items-end">
          <div>
            <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Explore Along the River')}
            </h2>
          </div>
          <div
            role="tablist"
            className="flex flex-wrap gap-1 rounded-full bg-surface-container p-1"
          >
            <button
              type="button"
              role="tab"
              aria-selected={filter === 'all'}
              onClick={() => setFilter('all')}
              className={pill(filter === 'all')}
            >
              {t('All POIs')}
            </button>
            {categories.map((c) => (
              <button
                key={c}
                type="button"
                role="tab"
                aria-selected={filter === c}
                onClick={() => setFilter(c)}
                className={pill(filter === c)}
              >
                {c}
              </button>
            ))}
          </div>
        </div>
        <div className="grid grid-cols-1 gap-space-md sm:grid-cols-2 lg:grid-cols-4">
          {visible.map((l) => (
            <Link
              key={l.id}
              to={ROUTES.liveTracking}
              data-reveal
              className="group overflow-hidden rounded-2xl border border-outline-variant/30 bg-white shadow-sm transition-all hover:shadow-lg"
            >
              <div className="relative h-40 overflow-hidden">
                <img
                  alt={l.title}
                  src={l.image}
                  className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
                />
                <span className="absolute left-3 top-3 rounded-full bg-white/90 px-2.5 py-0.5 text-[11px] font-semibold text-deep-river">
                  {l.category}
                </span>
              </div>
              <div className="space-y-1 p-space-md">
                <div className="text-[11px] font-semibold text-teal-flow">{l.routeTag}</div>
                <h3 className="font-headline-sm text-lg font-bold text-deep-river">{l.title}</h3>
                <div className="flex items-center gap-1 pt-1 text-xs font-semibold text-teal-flow">
                  {t('Discover Story')} <Icon name="arrow_forward" className="text-[14px]" />
                </div>
              </div>
            </Link>
          ))}
        </div>
      </div>
    </section>
  )
}

export function StoriesSection() {
  return (
    <section className="bg-deep-river py-space-2xl text-on-primary">
      <div className={`${SECTION} flex items-center gap-space-md`}>
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-teal-flow/25 text-sky-aqua">
          <Icon name="campaign" className="text-[26px]" />
        </span>
        <div>
          <h2 className="font-headline-sm text-headline-sm font-bold">
            {t('Stories on board')}
          </h2>
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
          <div className="max-w-xl">
            <h2 className="mt-1 font-headline-lg text-headline-lg tracking-tight">
              {t('Choose a Journey and See the River Differently')}
            </h2>
          </div>
          <div className="flex flex-wrap gap-space-sm">
            <Link
              to={ROUTES.search}
              className="inline-flex items-center gap-2 rounded-xl bg-teal-flow px-6 py-3 font-headline-sm text-sm font-semibold text-on-primary shadow-lg transition-colors hover:bg-secondary"
            >
              {t('Plan a Sightseeing Journey')}{' '}
              <Icon name="arrow_forward" className="text-[18px]" />
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
