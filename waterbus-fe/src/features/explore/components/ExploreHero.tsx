import { t } from '../../../i18n'
import { SECTION } from '../constants'
import SightseeingCta from './SightseeingCta'

export default function ExploreHero() {
  return (
    <section className="bg-gradient-to-br from-deep-river via-deep-river to-teal-flow text-on-primary">
      <div className={`${SECTION} py-space-3xl`}>
        <h1 className="max-w-2xl font-display text-4xl font-bold leading-tight tracking-tight md:text-headline-xl">
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
