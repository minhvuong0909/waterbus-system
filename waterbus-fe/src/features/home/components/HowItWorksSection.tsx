import type { Step } from '../types'
import { t } from '../../../i18n'

interface HowItWorksSectionProps {
  steps: Step[]
}

export default function HowItWorksSection({ steps }: HowItWorksSectionProps) {
  return (
    <section id="how-it-works" className="w-full scroll-mt-20 border-y border-outline-variant/30 bg-sand-light/40 py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-8 px-5 sm:px-8">
        <div data-reveal className="mx-auto max-w-xl space-y-space-xs text-center">
          <h2 className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]">
            {t('How It Works')}
          </h2>
        </div>

        <div className="relative grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div
            aria-hidden="true"
            className="absolute left-16 right-16 top-7 z-0 hidden h-px bg-outline-variant/50 lg:block"
          />
          {steps.map((step) => (
            <div
              key={step.number}
              data-reveal
              className="relative z-10 flex items-start gap-4 rounded-2xl border border-white/80 bg-white/70 p-5 lg:flex-col lg:items-center lg:text-center"
            >
              <div
                className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl font-headline-sm text-base font-bold ${step.numberClass}`}
              >
                {step.number}
              </div>
              <div className="space-y-space-xs">
                <h3 className="font-headline-sm text-base font-bold text-deep-river">
                  {step.title}
                </h3>
                <p className="text-sm leading-relaxed text-on-surface-variant">
                  {step.description}
                </p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
