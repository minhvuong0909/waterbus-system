import { Link } from 'react-router-dom'
import heroVideo from '../../../assets/videos/saigon-waterbus.mp4'
import heroPoster from '../../../assets/videos/saigon-waterbus-poster.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { HeroCue } from '../types'
import { t } from '../../../i18n'

interface HeroSectionProps {
  cues: HeroCue[]
}

export default function HeroSection({ cues }: HeroSectionProps) {
  return (
    <section className="relative flex min-h-[720px] w-full flex-col justify-between overflow-hidden bg-gradient-to-b from-mist via-[#EDF4F5] to-surface lg:h-[86vh] lg:max-h-[920px] lg:min-h-[780px]">
      <div className="pointer-events-none absolute inset-0 z-0 overflow-hidden">
        <video
          aria-label={t(
            'Smart Waterbus electric catamaran gliding across the Saigon River with the city skyline',
          )}
          className="h-full w-full object-cover object-[62%_center] saturate-[1.08] lg:object-[68%_center]"
          src={heroVideo}
          poster={heroPoster}
          autoPlay
          muted
          loop
          playsInline
          preload="auto"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-deep-river/85 via-deep-river/55 to-transparent lg:w-[72%]" />
        <div className="absolute inset-0 bg-gradient-to-t from-deep-river/85 via-transparent to-deep-river/25" />
      </div>

      <div className="relative z-10 mx-auto w-full max-w-7xl px-margin pt-10 sm:pt-14 lg:pt-20">
        <div className="max-w-2xl space-y-space-md">
          <span className="inline-flex items-center gap-2 font-headline-sm text-sm font-semibold uppercase tracking-widest text-sky-aqua">
            {t('City Moves Differently from the River')}
          </span>
          <div className="relative space-y-1">
            <div className="inline-block -rotate-3 translate-y-1 select-none font-script text-3xl font-semibold text-sky-aqua drop-shadow-sm sm:text-4xl">
              {t('More than a ride ~')}
            </div>
            <h1 className="font-headline-xl text-4xl font-extrabold leading-[1.08] tracking-tight text-white drop-shadow-[0_2px_16px_rgba(13,37,56,0.45)] sm:text-5xl lg:text-headline-xl">
              {t('A Smarter Journey,')}
              <br />
              <span className="text-sky-aqua">{t('A Brighter Saigon')}</span>
            </h1>
          </div>
          <p className="max-w-xl pt-1 text-base leading-relaxed text-white/90 drop-shadow-[0_1px_8px_rgba(13,37,56,0.5)] sm:text-body-lg">
            {t(
              'Explore the city by water with real-time updates, seamless booking and unforgettable experiences.',
            )}
          </p>
          <div className="flex flex-wrap items-center gap-4 pt-3">
            <Link
              to={ROUTES.search}
              className="group inline-flex items-center justify-center gap-2.5 rounded-xl bg-teal-flow px-7 py-3.5 font-headline-sm text-base font-semibold text-on-primary shadow-[0_12px_24px_-8px_rgba(20,122,126,0.42)] transition-all hover:-translate-y-0.5 hover:bg-secondary hover:shadow-[0_16px_32px_-6px_rgba(20,122,126,0.55)]"
            >
              <span>{t('Explore Routes')}</span>
              <Icon
                name="arrow_forward"
                className="text-[20px] transition-transform group-hover:translate-x-1"
              />
            </Link>
            <Link
              to={ROUTES.explore}
              className="inline-flex items-center justify-center gap-2 rounded-xl border border-white/40 bg-white/15 px-7 py-3.5 font-headline-sm text-base font-semibold text-white shadow-md backdrop-blur-md transition-all hover:bg-white/25 hover:shadow-lg"
            >
              {t('Discover Experience')}
            </Link>
          </div>
        </div>
      </div>

      <div className="relative z-20 mx-auto w-full max-w-7xl px-margin pb-6 pt-12 sm:pb-8">
        <div className="flex flex-col justify-between gap-4 rounded-2xl border border-white/15 bg-deep-river/75 p-4 text-on-primary shadow-[0_20px_40px_rgba(13,37,56,0.35)] backdrop-blur-xl sm:p-5 lg:flex-row lg:items-center">
          <div className="grid flex-1 grid-cols-2 gap-4 sm:gap-6 md:grid-cols-4">
            {cues.map((cue) => (
              <div key={cue.title} className="flex items-center gap-3">
                <div
                  className={`flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl border border-white/10 bg-white/10 ${cue.iconClass}`}
                >
                  <Icon name={cue.icon} className="text-[22px]" />
                </div>
                <div className="min-w-0">
                  <div className="truncate font-headline-sm text-sm font-semibold text-white">
                    {cue.title}
                  </div>
                  <div className="truncate text-xs text-sand-light/80">{cue.subtitle}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
