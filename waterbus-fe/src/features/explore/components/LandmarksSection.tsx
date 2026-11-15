import { useState } from 'react'
import type { ExploreData, Landmark } from '../types'
import { t } from '../../../i18n'
import LandmarkDrawer from './LandmarkDrawer'
import LandmarkImage from './LandmarkImage'
import { SECTION } from '../constants'

export default function LandmarksSection({
  landmarks,
}: {
  landmarks: ExploreData['landmarks']
}) {
  const [open, setOpen] = useState<Landmark | null>(null)

  return (
    <section id="river-landmarks" className="scroll-mt-24 bg-surface py-16 sm:py-20 lg:py-24">
      <div className={SECTION}>
        <div className="mb-10 max-w-2xl sm:mb-14 lg:mb-16">
          <p className="mb-3 text-xs font-semibold uppercase tracking-[0.2em] text-teal-flow">
            {t('Landmarks')}
          </p>
          <h2 className="font-display text-4xl font-semibold tracking-tight text-deep-river sm:text-5xl">
            {t('Explore Along the River')}
          </h2>
        </div>
        {landmarks.length === 0 && (
          <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-lg text-center text-sm text-on-surface-variant">
            {t('No landmarks are available yet.')}
          </p>
        )}
        <div className="space-y-12 sm:space-y-16 lg:space-y-20">
          {landmarks.map((landmark, index) => {
            const isReversed = index % 2 === 1
            const number = String(index + 1).padStart(2, '0')
            const copyBackground = isReversed ? 'bg-sand-light' : 'bg-white'

            return (
              <button
                key={landmark.id}
                type="button"
                onClick={() => setOpen(landmark)}
                aria-label={`${t('Details')}: ${landmark.title}`}
                aria-haspopup="dialog"
                data-reveal="landmark"
                data-reveal-direction={isReversed ? 'right' : 'left'}
                className="group grid w-full cursor-pointer grid-cols-1 items-stretch text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-teal-flow lg:grid-cols-12"
              >
                <div
                  data-reveal-part="media"
                  className={`relative isolate aspect-[4/3] overflow-hidden bg-secondary-container sm:aspect-[16/10] lg:col-span-7 lg:aspect-auto lg:min-h-[34rem] ${
                    isReversed ? 'lg:col-start-6 lg:row-start-1' : ''
                  }`}
                >
                  <LandmarkImage
                    landmark={landmark}
                    className="absolute inset-0 h-full w-full transition-transform duration-700 ease-out group-hover:scale-[1.035] motion-reduce:transform-none"
                  />
                  <span
                    aria-hidden="true"
                    className="pointer-events-none absolute inset-0 bg-gradient-to-t from-deep-river/55 via-transparent to-transparent"
                  />
                  <span
                    aria-hidden="true"
                    className="absolute bottom-5 left-5 font-display text-5xl font-semibold leading-none text-white drop-shadow sm:bottom-7 sm:left-7 sm:text-6xl"
                  >
                    {number}
                  </span>
                </div>
                <div
                  data-reveal-part="copy"
                  className={`flex flex-col items-start justify-center gap-5 px-6 py-9 sm:gap-6 sm:px-10 sm:py-12 lg:col-span-5 lg:px-12 lg:py-14 ${
                    isReversed
                      ? 'lg:col-start-1 lg:row-start-1'
                      : 'lg:col-start-8 lg:row-start-1'
                  } ${copyBackground}`}
                >
                  <p className="text-xs font-semibold uppercase tracking-[0.2em] text-teal-flow">
                    {t(landmark.category)}
                  </p>
                  <h3 className="max-w-xl font-display text-4xl font-semibold leading-[1.05] tracking-tight text-deep-river transition-colors duration-300 group-hover:text-teal-flow sm:text-5xl lg:text-6xl">
                    {landmark.title}
                  </h3>
                  <p className="max-w-lg text-base leading-relaxed text-on-surface-variant sm:text-lg">
                    {landmark.description}
                  </p>
                  <span
                    aria-hidden="true"
                    className="mt-1 h-px w-14 bg-teal-flow transition-[width] duration-300 group-hover:w-20"
                  />
                </div>
              </button>
            )
          })}
        </div>
      </div>
      {open && <LandmarkDrawer landmark={open} onClose={() => setOpen(null)} />}
    </section>
  )
}
