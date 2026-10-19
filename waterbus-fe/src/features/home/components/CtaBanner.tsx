import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

export default function CtaBanner() {
  return (
    <section className="w-full bg-surface py-space-3xl">
      <div className="mx-auto max-w-7xl px-margin">
        <div
          data-reveal
          className="pointer-glow relative overflow-hidden rounded-[24px] bg-gradient-to-r from-deep-river via-primary to-teal-flow px-6 py-9 text-on-primary shadow-lg sm:px-10 sm:py-12 lg:px-12"
        >
          <div aria-hidden="true" className="pointer-events-none absolute inset-0 opacity-[0.07]">
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
          <div className="relative z-10 flex flex-col justify-between gap-6 lg:flex-row lg:items-center lg:gap-10">
            <h2 className="max-w-2xl font-headline-lg text-2xl font-bold leading-tight tracking-tight text-on-primary sm:text-3xl lg:text-4xl">
              {t('Ready to Experience Saigon from the Water?')}
            </h2>
            <div className="flex shrink-0 items-center">
              <Link
                to={ROUTES.search}
                className="hover-lift group inline-flex items-center justify-center gap-space-sm rounded-xl bg-white px-6 py-3.5 font-headline-sm text-base font-semibold text-deep-river shadow-sm hover:bg-sand-light"
              >
                <span>{t('Plan Your Journey Now')}</span>
                <Icon
                  name="arrow_forward"
                  className="text-[20px] transition-transform duration-200 group-hover:translate-x-1"
                />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
