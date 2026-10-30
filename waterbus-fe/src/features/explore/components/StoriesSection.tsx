import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { SECTION } from '../constants'

export default function StoriesSection() {
  return (
    <section className="bg-deep-river py-space-2xl text-on-primary">
      <div className={`${SECTION} flex items-center gap-space-md`}>
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-teal-flow/25 text-sky-aqua">
          <Icon name="campaign" className="text-2xl" />
        </span>
        <div>
          <h2 className="font-display text-headline-sm font-bold">{t('Stories on board')}</h2>
          <p className="text-body-md text-sand-light/80">
            {t('On sightseeing journeys, stories about the places you pass play over the boat’s speakers.')}
          </p>
        </div>
      </div>
    </section>
  )
}
