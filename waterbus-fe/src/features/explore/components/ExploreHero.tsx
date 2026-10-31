import vesselImage from '../../../assets/images/vessel.jpg'
import { t } from '../../../i18n'
import { SECTION } from '../constants'
import SightseeingCta from './SightseeingCta'

export default function ExploreHero() {
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
