import heroImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import type { AuthShowcase } from '../authenticationTypes'

interface AuthShowcasePanelProps {
  showcase: AuthShowcase
}

export default function AuthShowcasePanel({ showcase }: AuthShowcasePanelProps) {
  const { crossing } = showcase

  return (
    <div className="relative flex flex-col justify-between overflow-hidden bg-deep-river p-8 text-white sm:p-10 lg:col-span-5">
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

      <div className="relative z-10">
        <div className="mb-6 inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/10 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider text-sky-aqua backdrop-blur-md">
          <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-sky-aqua" />
          {showcase.eyebrow}
        </div>
        <h1 className="mb-3 font-headline-xl text-3xl font-extrabold leading-[1.15] tracking-tight text-white sm:text-4xl">
          {showcase.headline[0]} <br />
          <span className="text-sky-aqua">{showcase.headline[1]}</span>
        </h1>
        <p className="max-w-sm text-sm leading-relaxed text-outline-variant">{showcase.intro}</p>
      </div>

      <div className="relative z-10 my-8 rounded-xl border border-white/10 bg-white/5 p-4 backdrop-blur-sm">
        <div className="mb-3 flex items-center justify-between text-xs font-semibold text-outline-variant">
          <span className="flex items-center gap-1.5 text-sky-aqua">
            <Icon name="radio_button_checked" className="text-[14px] text-teal-flow" />
            {crossing.corridor}
          </span>
          <span className="font-mono text-[11px] text-outline">{crossing.duration}</span>
        </div>
        <div className="relative flex items-center justify-between pb-2 pt-1">
          <div className="absolute inset-x-3 top-1/2 h-0.5 -translate-y-1/2 bg-on-surface-variant" />
          <div className="absolute left-3 top-1/2 h-0.5 w-1/2 -translate-y-1/2 bg-teal-flow" />
          <div className="relative flex flex-col items-center">
            <div className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-white bg-teal-flow shadow-sm" />
            <span className="mt-1.5 text-[10px] font-semibold text-surface-container">
              {crossing.from}
            </span>
          </div>
          <div className="relative -mt-1 flex flex-col items-center">
            <div className="flex items-center gap-1 rounded-full bg-sky-aqua px-2 py-0.5 text-[10px] font-bold text-deep-river shadow-md">
              <Icon name="directions_boat" className="text-[12px]" />
              <span>{crossing.vessel}</span>
            </div>
          </div>
          <div className="relative flex flex-col items-center">
            <div className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-outline bg-on-surface-variant" />
            <span className="mt-1.5 text-[10px] font-semibold text-outline">{crossing.to}</span>
          </div>
        </div>
      </div>

      <ul className="relative z-10 flex flex-col gap-2.5 border-t border-white/10 pt-4">
        {showcase.highlights.map((text) => (
          <li key={text} className="flex items-center gap-2.5 text-xs text-outline-variant">
            <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-teal-flow/30 text-sky-aqua">
              <Icon name="check" className="text-[12px]" />
            </span>
            {text}
          </li>
        ))}
      </ul>
    </div>
  )
}
