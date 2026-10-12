import { useState } from 'react'
import { Link } from 'react-router-dom'
import vesselImage from '../../../assets/images/vessel.jpg'
import AudioPlayer from '../../../components/audio/AudioPlayer'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { ExploreData, PoiCategory } from '../types'
import { t } from '../../../i18n'

const SECTION = 'mx-auto max-w-7xl px-margin'

export function ExploreHero({ onStories }: { onStories: () => void }) {
  const cues = [
    { icon: 'alt_route', title: 'River Journeys', text: 'Curated routes along the river' },
    { icon: 'headphones', title: 'Audio Stories', text: 'Contextual river storytelling' },
    { icon: 'location_city', title: 'City Discovery', text: 'Experience waterfront landmarks' },
  ]
  return (
    <section className="relative overflow-hidden bg-deep-river text-on-primary">
      <img
        alt=""
        aria-hidden="true"
        src={vesselImage}
        className="absolute inset-0 h-full w-full object-cover opacity-25 mix-blend-luminosity"
      />
      <div className="absolute inset-0 bg-gradient-to-b from-deep-river/70 via-deep-river/85 to-deep-river" />
      <div className={`${SECTION} relative z-10 pb-space-xl pt-space-3xl`}>
        <span className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-sky-aqua">
          <span className="h-1.5 w-1.5 rounded-full bg-sky-aqua" />
          {t('Sightseeing & River Discovery')}
        </span>
        <h1 className="mt-space-md max-w-2xl font-headline-xl text-4xl font-bold leading-tight tracking-tight md:text-headline-xl">
          {t('See the City Differently from the River')}
        </h1>
        <p className="mt-space-md max-w-xl text-body-lg text-sand-light/85">
          {t(
            'Curated catamaran journeys connecting modern Saigon with historic waterfront landmarks through contextual stories and scenic cruising.',
          )}
        </p>
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
          <button
            type="button"
            onClick={onStories}
            className="inline-flex items-center gap-2 rounded-xl border border-white/20 bg-white/10 px-6 py-3 font-headline-sm text-sm font-semibold text-white backdrop-blur-sm transition-colors hover:bg-white/20"
          >
            <Icon name="graphic_eq" className="text-[18px]" />
            {t('Discover River Stories')}
          </button>
        </div>
        <div className="mt-space-2xl grid grid-cols-1 gap-space-md md:grid-cols-3">
          {cues.map((c) => (
            <div
              key={c.title}
              className="flex items-center gap-3 rounded-xl border border-white/10 bg-white/5 p-space-md backdrop-blur-sm"
            >
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-teal-flow/25 text-sky-aqua">
                <Icon name={c.icon} className="text-[22px]" />
              </span>
              <div>
                <div className="font-headline-sm text-sm font-semibold">{t(c.title)}</div>
                <div className="text-xs text-sand-light/70">{t(c.text)}</div>
              </div>
            </div>
          ))}
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
            <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-teal-flow">
              <Icon name="explore" className="text-[16px]" /> {t('Signature Experiences')}
            </span>
            <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Sightseeing Journeys')}
            </h2>
            <p className="max-w-xl text-body-md text-on-surface-variant">
              {t(
                'Curated river experiences designed for leisure, photography, and cultural discovery along the urban waterway.',
              )}
            </p>
          </div>
          <Link
            to={ROUTES.searchResults}
            className="flex items-center gap-1 text-sm font-semibold text-teal-flow hover:underline"
          >
            {t('Browse All Journeys')} <Icon name="arrow_forward" className="text-[16px]" />
          </Link>
        </div>
        <div className="grid grid-cols-1 gap-space-lg md:grid-cols-3">
          {journeys.map((j) => (
            <Link
              key={j.id}
              to={ROUTES.searchResults}
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
                <p className="text-sm text-on-surface-variant">{j.description}</p>
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

export function ModesSection({ modes }: { modes: ExploreData['modes'] }) {
  return (
    <section className="border-y border-outline-variant/30 bg-sand-light/50 py-space-3xl">
      <div className={SECTION}>
        <div className="mx-auto mb-space-xl max-w-2xl text-center">
          <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
            {t('Waterway Mobility Modes')}
          </span>
          <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
            {t('Two Ways to Experience the River')}
          </h2>
          <p className="text-body-md text-on-surface-variant">
            {t(
              'Whether you need efficient daily transport or an unhurried storytelling voyage, Smart Waterbus accommodates your pace.',
            )}
          </p>
        </div>
        <div className="mx-auto grid max-w-4xl grid-cols-1 gap-space-lg md:grid-cols-2">
          {modes.map((m) => {
            const dark = m.id === 'sightseeing'
            return (
              <div
                key={m.id}
                className={`flex flex-col rounded-panel p-space-lg shadow-sm ${
                  dark
                    ? 'bg-deep-river text-on-primary shadow-xl'
                    : 'border border-outline-variant/30 bg-white'
                }`}
              >
                <div className="flex items-start justify-between">
                  <span
                    className={`flex h-10 w-10 items-center justify-center rounded-lg ${
                      dark ? 'bg-white/10 text-sky-aqua' : 'bg-mist text-teal-flow'
                    }`}
                  >
                    <Icon name={m.icon} className="text-[22px]" />
                  </span>
                  <span
                    className={`rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ${
                      dark
                        ? 'bg-coral-glow/20 text-coral-glow'
                        : 'bg-surface-container text-on-surface-variant'
                    }`}
                  >
                    {m.chip}
                  </span>
                </div>
                <h3 className="mt-space-md font-headline-sm text-2xl font-bold">{m.title}</h3>
                <div
                  className={`text-sm font-semibold ${dark ? 'text-sky-aqua' : 'text-teal-flow'}`}
                >
                  {m.subtitle}
                </div>
                <ul className="mt-space-md flex-1 space-y-2 text-sm">
                  {m.features.map((f) => (
                    <li
                      key={f}
                      className={`flex items-start gap-2 ${
                        dark ? 'text-sand-light/90' : 'text-on-surface-variant'
                      }`}
                    >
                      <Icon
                        name="check_circle"
                        className={`mt-0.5 text-[18px] ${
                          dark ? 'text-sky-aqua' : 'text-teal-flow'
                        }`}
                      />
                      {f}
                    </li>
                  ))}
                </ul>
                <Link
                  to={ROUTES.searchResults}
                  className={`mt-space-lg rounded-xl py-3 text-center text-sm font-semibold transition-colors ${
                    dark
                      ? 'bg-teal-flow text-on-primary hover:bg-secondary'
                      : 'bg-surface-container text-deep-river hover:bg-surface-container-high'
                  }`}
                >
                  {m.cta}
                </Link>
              </div>
            )
          })}
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
            <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-teal-flow">
              <Icon name="location_city" className="text-[16px]" /> {t('Waterfront Landmarks')}
            </span>
            <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Explore Along the River')}
            </h2>
            <p className="text-body-md text-on-surface-variant">
              {t(
                'Icons, historic quays, and contemporary architecture along our waterway corridors.',
              )}
            </p>
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
                <p className="text-xs text-on-surface-variant">{l.description}</p>
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

export function StoriesSection({
  data,
  sectionRef,
}: {
  data: ExploreData
  sectionRef: React.RefObject<HTMLElement>
}) {
  const { episode, languages } = data
  return (
    <section
      ref={sectionRef}
      className="relative overflow-hidden bg-deep-river py-space-3xl text-on-primary"
    >
      <div
        className={`${SECTION} relative z-10 grid grid-cols-1 items-center gap-space-xl lg:grid-cols-2`}
      >
        <div>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-teal-flow/20 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-sky-aqua">
            <Icon name="headphones" className="text-[14px]" />{' '}
            {t('Available on Sightseeing Journeys')}
          </span>
          <h2 className="mt-space-md font-headline-lg text-headline-xl tracking-tight">
            {t('Stories That Travel With You')}
          </h2>
          <p className="mt-space-sm max-w-md text-body-md text-sand-light/80">
            {t(
              'Discover curated narrative episodes that share the history, culture, and architecture of landmarks along your route.',
            )}
          </p>
          <div className="mt-space-lg grid grid-cols-1 gap-space-sm sm:grid-cols-2">
            <div className="rounded-xl border border-white/10 bg-white/5 p-space-md">
              <div className="font-headline-sm text-sm font-bold text-sky-aqua">
                {t('Route Connected')}
              </div>
              <p className="text-xs text-sand-light/70">
                {t('Connected to places along the sightseeing route.')}
              </p>
            </div>
            <div className="rounded-xl border border-white/10 bg-white/5 p-space-md">
              <div className="font-headline-sm text-sm font-bold text-coral-glow">
                {t('Contextual Audio')}
              </div>
              <p className="text-xs text-sand-light/70">
                {t('Available during selected sightseeing journeys.')}
              </p>
            </div>
          </div>
        </div>
        <div>
          <div className="mb-2 text-xs font-semibold text-sand-light/70">
            {t('Audio Narration Language:')}
          </div>
          <AudioPlayer
            key={episode.id}
            tone="dark"
            eyebrow={episode.eyebrow}
            title={episode.title}
            subtitle={episode.subtitle}
            thumbnail={episode.thumbnail}
            durationSec={episode.durationSec}
            startAtSec={episode.startAtSec}
            languages={languages}
          />
          <p className="mt-space-sm flex items-center gap-2 text-xs text-sand-light/70">
            <Icon name="info" className="text-[15px]" />
            {t('Audio stories may be available as you explore selected sightseeing routes.')}
          </p>
        </div>
      </div>
    </section>
  )
}

export function CorridorSection({ data }: { data: ExploreData }) {
  return (
    <section className="bg-surface py-space-3xl">
      <div className={SECTION}>
        <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Navigation & Landmarks')}
        </span>
        <div className="flex flex-col justify-between gap-space-sm md:flex-row md:items-end">
          <div>
            <h2 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Saigon River Discovery Corridor')}
            </h2>
            <p className="text-body-md text-on-surface-variant">
              {t(
                'Trace our scenic cruising path connecting central piers with historic cultural quays.',
              )}
            </p>
          </div>
          <div className="flex gap-2 text-xs font-semibold">
            <span className="flex items-center gap-1.5 rounded-full bg-sand-light px-3 py-1 text-teal-flow">
              <span className="h-2 w-2 rounded-full bg-teal-flow" /> {t('Active Sightseeing Route')}
            </span>
            <span className="flex items-center gap-1.5 rounded-full bg-surface-container px-3 py-1 text-on-surface-variant">
              <span className="h-2 w-2 rounded-full bg-coral-glow" />{' '}
              {t('Curated POI Pins ({count})', {
                count: data.pins.filter((p) => p.emphasis === 'poi').length + 2,
              })}
            </span>
          </div>
        </div>

        <div className="relative mt-space-lg h-[420px] overflow-hidden rounded-panel bg-deep-river">
          <svg
            className="absolute inset-0 h-full w-full"
            viewBox="0 0 1000 420"
            preserveAspectRatio="none"
            aria-hidden="true"
          >
            <defs>
              <linearGradient id="exploreRiver" x1="0" y1="1" x2="1" y2="0">
                <stop offset="0" stopColor="#147A7E" stopOpacity="0.55" />
                <stop offset="1" stopColor="#4FC3D8" stopOpacity="0.85" />
              </linearGradient>
            </defs>
            <path
              d="M0 360 C 200 330 300 250 460 210 C 620 170 760 130 1000 40 L 1000 210 C 780 250 640 300 480 330 C 320 360 160 420 0 420 Z"
              fill="url(#exploreRiver)"
            />
            <path
              d="M0 390 C 200 360 320 285 470 260 C 640 230 780 170 1000 125"
              fill="none"
              stroke="#fff"
              strokeOpacity="0.55"
              strokeWidth="3"
              strokeDasharray="10 10"
            />
          </svg>
          {data.pins.map((p) => (
            <div
              key={p.id}
              className="absolute -translate-x-1/2 -translate-y-1/2"
              style={{ left: `${p.x}%`, top: `${p.y}%` }}
            >
              <div className="flex flex-col items-center gap-1">
                <span
                  className={`flex items-center justify-center rounded-full border-2 text-white shadow-lg ${
                    p.emphasis === 'poi'
                      ? 'h-9 w-9 border-coral-glow bg-deep-river'
                      : 'h-10 w-10 border-white bg-teal-flow'
                  }`}
                >
                  <Icon name={p.icon} className="text-[18px]" />
                </span>
                <span className="whitespace-nowrap rounded-lg bg-white/95 px-2.5 py-1 text-center text-[11px] font-bold text-deep-river shadow">
                  {p.title}
                  {p.subtitle && (
                    <span className="block font-normal text-teal-flow">{p.subtitle}</span>
                  )}
                </span>
              </div>
            </div>
          ))}
          <div className="absolute left-1/2 top-[30%] hidden -translate-x-1/2 items-center gap-2 rounded-xl bg-white/95 px-3 py-2 shadow-lg md:flex">
            <Icon name="navigation" className="text-[18px] text-teal-flow" />
            <div>
              <div className="text-[11px] font-bold text-deep-river">
                {t('Active Sightseeing Corridor')}
              </div>
              <div className="text-[10px] text-on-surface-variant">{t('Curated Scenic Route')}</div>
            </div>
          </div>
          <div className="absolute bottom-4 right-4 flex flex-wrap items-center gap-3 rounded-xl bg-white/95 px-3 py-2 text-[11px] font-semibold text-deep-river shadow">
            <span className="flex items-center gap-1">
              <Icon name="route" className="text-[14px] text-teal-flow" />
              {t('Saigon River Discovery Corridor')}
            </span>
            <span className="flex items-center gap-1">
              <Icon name="pin_drop" className="text-[14px] text-coral-glow" />
              {t('4 Signature Points of Interest')}
            </span>
            <span className="flex items-center gap-1">
              <Icon name="explore" className="text-[14px] text-teal-flow" />
              {t('Scenic Route')}
            </span>
          </div>
        </div>
      </div>
    </section>
  )
}

export function StepAboardBanner() {
  return (
    <section className="bg-surface pb-space-3xl">
      <div className={SECTION}>
        <div className="relative flex flex-col items-start justify-between gap-space-lg overflow-hidden rounded-panel bg-gradient-to-r from-deep-river to-teal-flow p-space-2xl text-on-primary md:flex-row md:items-center">
          <div className="max-w-xl">
            <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-sky-aqua">
              <Icon name="sailing" className="text-[16px]" /> {t('Step Aboard')}
            </span>
            <h2 className="mt-1 font-headline-lg text-headline-lg tracking-tight">
              {t('Choose a Journey and See the River Differently')}
            </h2>
            <p className="mt-space-sm text-body-md text-sand-light/85">
              {t(
                'Discover iconic landmarks and scenic riverbanks from the tranquil comfort of Smart Waterbus.',
              )}
            </p>
          </div>
          <div className="flex flex-wrap gap-space-sm">
            <Link
              to={ROUTES.search}
              className="inline-flex items-center gap-2 rounded-xl bg-teal-flow px-6 py-3 font-headline-sm text-sm font-semibold text-on-primary shadow-lg transition-colors hover:bg-secondary"
            >
              {t('Plan a Sightseeing Journey')}{' '}
              <Icon name="arrow_forward" className="text-[18px]" />
            </Link>
            <Link
              to={ROUTES.search}
              className="inline-flex items-center rounded-xl border border-white/25 bg-white/10 px-6 py-3 font-headline-sm text-sm font-semibold text-white backdrop-blur-sm transition-colors hover:bg-white/20"
            >
              {t('View Routes')}
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
