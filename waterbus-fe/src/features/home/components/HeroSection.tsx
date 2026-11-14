import { t } from '../../../i18n'
import HeroSearch from './HeroSearch'
import HeroVideo from './HeroVideo'

/** One figure under the headline, e.g. "From 15.000 VND · per ticket". */
export interface HeroFact {
  value: string
  label: string
}

/**
 * First screen of the home page: the river video fills it, with what the service is and the figures
 * that matter on the left and the trip search along the bottom, all in view without scrolling.
 */
export default function HeroSection({ facts }: { facts: HeroFact[] }) {
  return (
    <section
      id="top"
      className="relative isolate flex min-h-[600px] flex-col bg-deep-river text-white lg:h-[calc(100svh-5rem)] lg:max-h-[860px]"
    >
      <HeroVideo />

      <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col justify-center px-5 pb-8 pt-12 sm:px-8">
        <p className="mb-4 inline-flex items-center gap-2 self-start rounded-full border border-white/30 bg-deep-river/40 px-3.5 py-1.5 text-2xs font-semibold uppercase tracking-[0.18em] text-white/95 backdrop-blur-sm">
          <span className="h-1.5 w-1.5 rounded-full bg-sky-aqua" />
          {t('Saigon River Waterbus')}
        </p>
        <h1 className="font-display text-4xl font-extrabold leading-[1.08] tracking-[-0.02em] drop-shadow-[0_2px_16px_rgba(13,37,56,0.5)] sm:text-5xl xl:text-6xl">
          <span className="block">{t('A Smarter Journey,')}</span>
          {/* pb giữ vùng nền gradient đủ sâu cho dấu nặng (Một, rạng), nếu không dấu bị cắt mất. */}
          <span className="-mb-[0.15em] block bg-gradient-to-r from-sky-aqua to-white bg-clip-text pb-[0.15em] text-transparent">
            {t('A Brighter Saigon')}
          </span>
        </h1>
        <p className="mt-4 max-w-xl text-body-lg text-white/90">
          {t('Pick a trip, choose your seat and pay online. No account needed.')}
        </p>

        {facts.length > 0 && (
          <dl className="mt-8 flex flex-wrap gap-x-8 gap-y-4">
            {facts.map((fact) => (
              <div key={fact.label} className="border-l-2 border-sky-aqua/70 pl-4">
                <dd className="font-display text-2xl font-bold leading-tight">{fact.value}</dd>
                <dt className="mt-0.5 text-xs text-white/80">{fact.label}</dt>
              </div>
            ))}
          </dl>
        )}
      </div>

      <div className="mx-auto w-full max-w-7xl px-5 pb-8 sm:px-8">
        <HeroSearch />
      </div>
    </section>
  )
}
