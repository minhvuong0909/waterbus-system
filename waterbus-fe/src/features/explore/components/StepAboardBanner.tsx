import { t } from '../../../i18n'
import SightseeingCta from './SightseeingCta'
import { SECTION } from '../constants'

export default function StepAboardBanner() {
  return (
    <section className="bg-surface py-space-xl">
      <div className={SECTION}>
        <div className="relative flex flex-col items-start justify-between gap-space-lg overflow-hidden rounded-panel bg-gradient-to-r from-deep-river to-teal-flow p-space-2xl text-on-primary md:flex-row md:items-center">
          <h2 className="max-w-xl font-display text-headline-lg tracking-tight">{t('Ready to come aboard?')}</h2>
          <SightseeingCta />
        </div>
      </div>
    </section>
  )
}
