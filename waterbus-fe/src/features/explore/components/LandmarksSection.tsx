import type { ExploreData } from '../types'
import { t } from '../../../i18n'
import LandmarkImage from './LandmarkImage'
import { SECTION } from '../constants'

export default function LandmarksSection({
  landmarks,
}: {
  landmarks: ExploreData['landmarks']
}) {
  return (
    <section id="river-landmarks" className="scroll-mt-24 bg-surface py-16 sm:py-20 lg:py-28">
      <div className={SECTION}>
        <div className="mb-12 max-w-2xl sm:mb-16 lg:mb-20">
          <p className="mb-3 flex items-center gap-3 text-xs font-semibold uppercase tracking-[0.2em] text-teal-flow">
            <span aria-hidden="true" className="h-px w-9 bg-teal-flow" />
            {t('Landmarks')}
          </p>
          <h2 className="font-display text-4xl font-bold tracking-tight text-deep-river [text-wrap:balance] sm:text-5xl lg:text-6xl">
            {t('Explore Along the River')}
          </h2>
          <p className="mt-5 text-base leading-relaxed text-on-surface-variant sm:text-lg">
            {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
          </p>
        </div>
        {landmarks.length === 0 && (
          <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-lg text-center text-sm text-on-surface-variant">
            {t('No landmarks are available yet.')}
          </p>
        )}
        <div className="space-y-14 sm:space-y-20 lg:space-y-28">
          {landmarks.map((landmark, index) => {
            // The first card (the flag) has its text on the left, so it never covers the flag on the right of the photo.
            const isReversed = index % 2 === 0
            const number = String(index + 1).padStart(2, '0')

            return (
              <article
                key={landmark.id}
                data-reveal="landmark"
                data-reveal-direction={isReversed ? 'right' : 'left'}
                className="group grid w-full grid-cols-1 items-center text-left lg:grid-cols-12"
              >
                {/* On wide screens the text card overlaps one column of the photo. */}
                <div
                  data-reveal-part="media"
                  className={`relative isolate aspect-[4/3] overflow-hidden rounded-3xl bg-secondary-container sm:aspect-[16/10] lg:col-span-7 lg:row-start-1 lg:aspect-auto lg:min-h-[32rem] ${
                    isReversed ? 'lg:col-start-6' : 'lg:col-start-1'
                  }`}
                >
                  <LandmarkImage
                    landmark={landmark}
                    className="absolute inset-0 h-full w-full transition-transform duration-[1400ms] ease-out group-hover:scale-105 motion-reduce:transition-none motion-reduce:group-hover:scale-100"
                  />
                </div>
                <div
                  data-reveal-part="copy"
                  className={`relative z-10 -mt-8 flex flex-col items-start gap-4 rounded-3xl bg-white p-7 shadow-raised sm:mx-8 sm:gap-5 sm:p-10 lg:col-span-6 lg:row-start-1 lg:m-0 lg:p-12 ${
                    isReversed ? 'lg:col-start-1' : 'lg:col-start-7'
                  }`}
                >
                  <div className="flex items-center gap-4">
                    <span
                      aria-hidden="true"
                      className="font-display text-5xl font-bold leading-none text-teal-flow/30 sm:text-6xl"
                    >
                      {number}
                    </span>
                    <p className="rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold uppercase tracking-[0.16em] text-on-secondary-container">
                      {t(landmark.category)}
                    </p>
                  </div>
                  <h3 className="font-display text-3xl font-bold leading-[1.1] tracking-tight text-deep-river [text-wrap:balance] sm:text-4xl lg:text-5xl">
                    {landmark.title}
                  </h3>
                  <p className="max-w-lg text-base leading-relaxed text-on-surface-variant sm:text-lg">
                    {landmark.description}
                  </p>
                </div>
              </article>
            )
          })}
        </div>
      </div>
    </section>
  )
}
