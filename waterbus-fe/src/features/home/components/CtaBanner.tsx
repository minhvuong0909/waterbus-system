import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function CtaBanner() {
  return (
    <section className="w-full bg-surface py-space-3xl">
      <div className="mx-auto max-w-7xl px-margin">
        <div className="relative overflow-hidden rounded-[28px] bg-gradient-to-r from-deep-river via-primary to-teal-flow p-space-2xl text-on-primary shadow-xl md:p-space-3xl">
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 opacity-15">
            <svg className="h-full w-full" fill="none" viewBox="0 0 1200 400">
              <path
                d="M0 200 Q 300 100, 600 200 T 1200 200"
                stroke="#FFFFFF"
                strokeLinecap="round"
                strokeWidth="40"
              />
              <path
                d="M0 260 Q 300 160, 600 260 T 1200 260"
                stroke="#4FC3D8"
                strokeDasharray="12 16"
                strokeWidth="20"
              />
            </svg>
          </div>
          <div className="relative z-10 max-w-2xl space-y-space-lg">
            <h2 className="font-headline-lg text-3xl font-bold leading-tight tracking-tight text-on-primary sm:text-headline-lg md:text-headline-xl">
              {t('Ready to Experience Saigon from the Water?')}
            </h2>
            <p className="max-w-xl text-body-lg text-sand-light/90">
              {t(
                'Effortless urban mobility, breezy commutes and breathtaking twilight river journeys.',
              )}
            </p>
            <div className="flex flex-wrap items-center gap-space-md pt-space-xs">
              <Link
                to={ROUTES.search}
                className="inline-flex items-center justify-center gap-space-sm rounded-xl bg-teal-flow px-space-xl py-4 font-headline-sm text-body-lg font-semibold text-on-primary shadow-lg transition-all hover:bg-secondary"
              >
                <span>{t('Plan Your Journey Now')}</span>
                <Icon name="arrow_forward" className="text-[20px]" />
              </Link>
              <Link
                to={ROUTES.searchResults}
                className="inline-flex items-center justify-center gap-space-xs rounded-xl border border-white/20 bg-surface/10 px-space-xl py-4 font-headline-sm text-body-lg font-semibold text-on-primary backdrop-blur-md transition-all hover:bg-surface/20"
              >
                <Icon name="calendar_month" className="text-[20px]" />
                <span>{t('View Live Timetable')}</span>
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
