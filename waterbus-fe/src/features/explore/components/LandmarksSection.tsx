import { useState } from 'react'
import { Icon } from '../../../components/ui'
import type { ExploreData, Landmark, PoiCategory } from '../types'
import { t } from '../../../i18n'
import LandmarkDrawer from './LandmarkDrawer'
import LandmarkImage from './LandmarkImage'
import { SECTION } from '../constants'

export default function LandmarksSection({
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
    `min-h-11 rounded-full px-4 py-2 text-sm transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow ${
      active
        ? 'bg-deep-river font-semibold text-white shadow-sm'
        : 'bg-white text-on-surface-variant hover:bg-white/70 hover:text-deep-river'
    }`

  return (
    <section id="river-landmarks" className="scroll-mt-24 bg-surface py-16 sm:py-20 lg:py-24">
      <div className={SECTION}>
        <div className="mb-8 flex flex-col justify-between gap-6 lg:mb-10 lg:flex-row lg:items-end">
          <div className="max-w-xl">
            <p className="mb-3 text-xs font-semibold uppercase tracking-[0.2em] text-teal-flow">
              {t('The city, one story at a time')}
            </p>
            <h2 className="font-display text-3xl font-bold tracking-tight text-deep-river sm:text-4xl">
              {t('Explore Along the River')}
            </h2>
          </div>
          <div
            role="group"
            aria-label={t('Filter by category')}
            className="flex w-fit max-w-full flex-wrap gap-1 rounded-full bg-surface-container p-1"
          >
            <button
              type="button"
              aria-pressed={filter === 'all'}
              onClick={() => setFilter('all')}
              className={pill(filter === 'all')}
            >
              {t('All')}
            </button>
            {categories.map((c) => (
              <button
                key={c}
                type="button"
                aria-pressed={filter === c}
                onClick={() => setFilter(c)}
                className={pill(filter === c)}
              >
                {t(c)}
              </button>
            ))}
          </div>
        </div>
        {visible.length === 0 && (
          <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-lg text-center text-sm text-on-surface-variant">
            {t('No places in this category yet.')}
          </p>
        )}
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 xl:grid-cols-3">
          {visible.map((l, index) => (
            <button
              key={l.id}
              type="button"
              onClick={() => setOpen(l)}
              aria-haspopup="dialog"
              data-reveal
              className={`group overflow-hidden rounded-3xl border border-outline-variant/30 bg-white text-left shadow-card transition-[transform,box-shadow,border-color] duration-300 hover:-translate-y-1 hover:border-teal-flow/30 hover:shadow-raised motion-reduce:transform-none focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow ${
                index === 0 ? 'sm:col-span-2 sm:grid sm:grid-cols-2' : ''
              }`}
            >
              <div className={`relative h-56 overflow-hidden ${index === 0 ? 'sm:h-full sm:min-h-72' : ''}`}>
                <LandmarkImage
                  landmark={l}
                  className="h-full w-full transition-transform duration-700 ease-out group-hover:scale-[1.04] motion-reduce:transform-none"
                />
                <span className="absolute left-4 top-4 rounded-full border border-white/70 bg-white/90 px-3 py-1 text-xs font-semibold text-deep-river shadow-sm backdrop-blur-sm">
                  {t(l.category)}
                </span>
                <span className="pointer-events-none absolute inset-x-0 bottom-0 h-20 bg-gradient-to-t from-deep-river/35 to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100 motion-reduce:transition-none" />
              </div>
              <div className={`flex flex-col justify-center p-5 sm:p-6 ${index === 0 ? 'sm:p-8' : ''}`}>
                <h3 className={`font-display font-bold leading-snug text-deep-river ${index === 0 ? 'text-2xl sm:text-3xl' : 'text-lg'}`}>
                  {l.title}
                </h3>
                <p className={`mt-3 line-clamp-2 text-sm leading-6 text-on-surface-variant ${index === 0 ? 'sm:text-base' : ''}`}>
                  {l.description}
                </p>
                <div className="mt-5 flex items-center gap-2 text-sm font-semibold text-teal-flow">
                  {t('Read the story')}
                  <Icon
                    name="arrow_forward"
                    className="text-base transition-transform duration-300 group-hover:translate-x-1 motion-reduce:transform-none"
                  />
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
