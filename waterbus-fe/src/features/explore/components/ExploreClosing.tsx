import { t } from '../../../i18n'
import boatImg from '../../../assets/images/waterbus.jpg'
import { SECTION } from '../constants'
import SightseeingCta from './SightseeingCta'

/** Closing call to action: after the stories, lead the visitor to the trips they can book. */
export default function ExploreClosing() {
  return (
    <section className="bg-surface pb-16 sm:pb-20 lg:pb-28">
      <div className={SECTION}>
        <div
          data-reveal
          className="relative isolate overflow-hidden rounded-3xl bg-deep-river px-6 py-14 text-center text-on-primary sm:px-12 sm:py-20"
        >
          <img
            src={boatImg}
            alt=""
            loading="lazy"
            decoding="async"
            className="absolute inset-0 -z-10 h-full w-full object-cover opacity-40"
          />
          <div aria-hidden="true" className="absolute inset-0 -z-10 bg-deep-river/70" />
          <h2 className="mx-auto max-w-2xl font-display text-4xl font-bold leading-[1.1] tracking-tight [text-wrap:balance] sm:text-5xl">
            {t('See the City Differently from the River')}
          </h2>
          <p className="mx-auto mt-5 max-w-xl text-base leading-relaxed text-white/85 sm:text-lg">
            {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
          </p>
          <div className="mt-8 flex justify-center">
            <SightseeingCta />
          </div>
        </div>
      </div>
    </section>
  )
}
