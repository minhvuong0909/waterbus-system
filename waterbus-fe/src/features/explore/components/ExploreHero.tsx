import baSonImg from '../../../assets/images/explore/ba-son.jpg'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { SECTION } from '../constants'
import SightseeingCta from './SightseeingCta'

export default function ExploreHero() {
  return (
    <section
      className="relative isolate flex min-h-[min(740px,calc(100svh-5rem))] overflow-hidden bg-deep-river text-on-primary"
    >
      <img
        src={baSonImg}
        alt=""
        aria-hidden="true"
        className="absolute inset-0 h-full w-full animate-hero-settle object-cover object-[58%_52%] motion-reduce:animate-none"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-r from-deep-river/95 via-deep-river/70 to-deep-river/10"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-t from-deep-river/65 via-transparent to-deep-river/10"
      />

      <div className={`${SECTION} relative flex w-full flex-col justify-center py-20 md:py-28`}>
        <div className="explore-hero__content max-w-3xl">
          <div className="flex items-center gap-3 text-xs font-semibold uppercase tracking-[0.22em] text-sand-light">
            <span className="h-px w-9 bg-sky-aqua" />
            {t('Discover Saigon from the river')}
          </div>
          <h1 className="mt-6 max-w-3xl font-display text-4xl font-bold leading-[1.08] tracking-[-0.04em] text-white sm:text-5xl md:text-6xl lg:text-7xl">
            {t('See the City Differently from the River')}
          </h1>
          <p className="mt-6 max-w-xl text-base leading-7 text-white/85 sm:text-lg sm:leading-8">
            {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
          </p>
          <div className="mt-8 flex flex-wrap items-center gap-3">
            <SightseeingCta />
            <a
              href="#river-landmarks"
              className="group inline-flex min-h-12 items-center gap-2 rounded-full border border-white/40 bg-white/10 px-5 text-sm font-semibold text-white backdrop-blur-sm transition-colors hover:border-white/70 hover:bg-white/20"
            >
              {t('Explore landmarks')}
              <Icon name="arrow_downward" className="text-lg transition-transform group-hover:translate-y-0.5 motion-reduce:transform-none" />
            </a>
          </div>
        </div>

        <div className="absolute bottom-7 right-margin hidden items-center gap-3 text-xs font-medium uppercase tracking-[0.18em] text-white/75 md:flex">
          <span className="h-px w-10 bg-white/50" />
          {t('Saigon River')}
        </div>
      </div>
    </section>
  )
}
