import heroImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { AUTH_SHOWCASE as showcase } from '../../../content/auth'
import { t } from '../../../i18n'

export default function AuthShowcasePanel() {
  return (
    <div className="relative hidden flex-col justify-between overflow-hidden bg-deep-river p-8 text-white lg:col-span-5 lg:flex lg:p-10">
      <img
        alt=""
        aria-hidden="true"
        className="absolute inset-0 h-full w-full scale-105 object-cover opacity-35 mix-blend-luminosity"
        src={heroImage}
      />
      <div className="absolute inset-0 bg-gradient-to-t from-deep-river via-deep-river/80 to-deep-river/40" />
      <div className="absolute inset-0 bg-gradient-to-r from-deep-river/60 via-transparent to-deep-river/70" />
      <svg
        className="pointer-events-none absolute -bottom-16 -right-20 h-96 w-96 stroke-current text-teal-flow/25"
        fill="none"
        strokeWidth="1.5"
        viewBox="0 0 400 400"
        aria-hidden="true"
      >
        <path d="M0,120 C140,80 200,280 400,220" />
        <path
          className="text-sky-aqua/30"
          d="M0,170 C150,130 210,330 400,270"
          strokeDasharray="4 4"
          strokeWidth="1"
        />
        <path d="M0,220 C160,180 220,380 400,320" />
      </svg>

      <div className="relative z-10 mb-8">
        <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-sky-aqua backdrop-blur-md">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-sky-aqua" />
          {t(showcase.eyebrow)}
        </div>
        <h2 className="mb-3 font-headline-xl text-3xl font-extrabold leading-[1.2] tracking-tight text-white xl:text-[34px]">
          {t(showcase.headline[0])} <br />
          <span className="text-sky-aqua">{t(showcase.headline[1])}</span>
        </h2>
        <p className="max-w-sm text-sm leading-relaxed text-outline-variant">{t(showcase.intro)}</p>
      </div>

      <ul className="relative z-10 flex flex-col gap-2.5 border-t border-white/10 pt-4">
        {showcase.highlights.map((text) => (
          <li key={text} className="flex items-center gap-2.5 text-xs text-outline-variant">
            <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-teal-flow/30 text-sky-aqua">
              <Icon name="check" className="text-[12px]" />
            </span>
            {t(text)}
          </li>
        ))}
      </ul>
    </div>
  )
}
