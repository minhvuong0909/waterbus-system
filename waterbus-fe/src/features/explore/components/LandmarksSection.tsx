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
