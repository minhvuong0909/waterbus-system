import type { Step } from '../types'
import { t } from '../../../i18n'

interface HowItWorksSectionProps {
  steps: Step[]
}

export default function HowItWorksSection({ steps }: HowItWorksSectionProps) {
  return (
    <section className="w-full border-y border-outline-variant/30 bg-sand-light/50 py-space-3xl">
      <div className="mx-auto max-w-7xl space-y-space-2xl px-margin">
        <div className="mx-auto max-w-xl space-y-space-xs text-center">
          <span className="block text-body-sm font-bold uppercase tracking-wider text-teal-flow">
            {t('Effortless Commute')}
          </span>
          <h2 className="font-headline-lg text-headline-lg font-bold tracking-tight text-deep-river">
            {t('How It Works')}
          </h2>
          <p className="text-body-md text-on-surface-variant">
            {t('From trip planning to boarding turnstiles in under sixty seconds.')}
          </p>
        </div>

        <div className="relative grid grid-cols-1 gap-space-lg md:grid-cols-4">
          <div
            aria-hidden="true"
            className="absolute left-16 right-16 top-7 z-0 hidden h-0.5 bg-outline-variant/60 md:block"
          />
          {steps.map((step) => (
            <div
              key={step.number}
              className="relative z-10 flex flex-col items-center space-y-space-md text-center"
            >
              <div
                className={`flex h-14 w-14 items-center justify-center rounded-2xl font-headline-sm text-body-lg font-bold shadow-md ${step.numberClass}`}
              >
                {step.number}
              </div>
              <div className="space-y-space-xs">
                <h3 className="font-headline-sm text-headline-sm font-bold text-deep-river">
                  {step.title}
                </h3>
                <p className="text-body-md text-on-surface-variant">{step.description}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
