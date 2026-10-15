import { Icon } from '../ui'
import type { CorridorMap, Pier } from '../../features/trips/types'
import { t } from '../../i18n'

interface RiverMapProps {
  map: CorridorMap
  className?: string
}

const PIER_STYLES: Record<Pier['kind'], { dot: string; label: string; sub: string }> = {
  hub: {
    dot: 'h-7 w-7 bg-deep-river ring-4 ring-deep-river/20',
    label: 'rounded-xl px-3 py-1.5 text-xs font-bold',
    sub: 'font-semibold text-teal-flow',
  },
  terminus: {
    dot: 'h-7 w-7 bg-deep-river ring-4 ring-deep-river/20',
    label: 'rounded-xl px-3 py-1.5 text-xs font-bold',
    sub: 'font-semibold text-teal-flow',
  },
  stop: {
    dot: 'h-5 w-5 bg-teal-flow',
    label: 'rounded-lg px-2.5 py-1 text-xs font-semibold',
    sub: 'text-on-surface-variant',
  },
}

const PIER_ICON: Partial<Record<Pier['kind'], string>> = { hub: 'anchor', terminus: 'flag' }

/**
 * Stylised river corridor map: SVG ribbon + absolutely positioned pier nodes and vessel pin.
 * Node positions are percentages so the map scales with its container.
 */
export default function RiverMap({ map, className = '' }: RiverMapProps) {
  const { piers, vessel } = map

  return (
    <div
      className={`relative flex h-[470px] w-full items-center justify-center overflow-hidden rounded-2xl border border-teal-flow/15 bg-[#EEF5F4] shadow-inner ${className}`.trim()}
    >
      <svg
        className="absolute inset-0 h-full w-full"
        fill="none"
        preserveAspectRatio="none"
        viewBox="0 0 700 460"
        aria-hidden="true"
      >
        <defs>
          <pattern height="35" id="river-mesh" patternUnits="userSpaceOnUse" width="35">
            <path
              d="M 35 0 L 0 0 0 35"
              fill="none"
              stroke="#D8E8E4"
              strokeDasharray="2 3"
              strokeWidth="0.75"
            />
          </pattern>
          <linearGradient id="saigon-waterway" x1="0%" x2="100%" y1="0%" y2="100%">
            <stop offset="0%" stopColor="#4FC3D8" stopOpacity="0.9" />
            <stop offset="45%" stopColor="#147A7E" stopOpacity="0.95" />
            <stop offset="100%" stopColor="#0D2538" stopOpacity="0.9" />
          </linearGradient>
        </defs>
        <rect fill="url(#river-mesh)" height="100%" width="100%" />
        <path
          d="M 20 60 C 170 80, 230 220, 360 210 C 490 200, 520 380, 680 390"
          opacity="0.4"
          stroke="#B2D8D6"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="68"
        />
        <path
          d="M 20 60 C 170 80, 230 220, 360 210 C 490 200, 520 380, 680 390"
          stroke="url(#saigon-waterway)"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="26"
        />
        <path
          d="M 20 60 C 170 80, 230 220, 360 210 C 490 200, 520 380, 680 390"
          stroke="#FFFFFF"
          strokeDasharray="8 12"
          strokeLinecap="round"
          strokeWidth="3.5"
        />
      </svg>

      {piers.map((pier) => {
        const style = PIER_STYLES[pier.kind]
        const icon = PIER_ICON[pier.kind]
        return (
          <div
            key={pier.id}
            className="absolute z-10 flex cursor-pointer items-center gap-space-xs"
            style={{ left: `${pier.x}%`, top: `${pier.y}%` }}
          >
            <div
              className={`flex items-center justify-center rounded-full text-on-primary shadow-lg ${style.dot}`}
            >
              {icon ? (
                <Icon name={icon} className="text-[15px]" />
              ) : (
                <span className="h-2 w-2 rounded-full bg-on-primary" />
              )}
            </div>
            <div
              className={`border border-outline-variant/30 bg-surface-container-lowest shadow-md ${style.label}`}
            >
              <span className="block font-headline-sm text-deep-river">{pier.name}</span>
              <span className={`text-[10px] ${style.sub}`}>{pier.subtitle}</span>
            </div>
          </div>
        )
      })}

      <div
        className="absolute z-20 -translate-x-1/2 -translate-y-1/2"
        style={{ left: `${vessel.x}%`, top: `${vessel.y}%` }}
      >
        <div className="relative flex flex-col items-center">
          <div className="mb-2 flex items-center gap-2.5 rounded-xl border border-white/20 bg-deep-river px-3 py-2 text-on-primary shadow-xl">
            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-teal-flow text-white">
              <Icon name="directions_boat" className="text-[18px]" />
            </div>
            <div className="text-left">
              <div className="font-headline-sm text-xs font-bold leading-tight">
                {vessel.code} • {vessel.status}
              </div>
              <div className="text-[11px] font-semibold text-sky-aqua">
                {t('{speedKmh} km/h • Next: {nextPier}', {
                  speedKmh: vessel.speedKmh,
                  nextPier: vessel.nextPier,
                })}
              </div>
            </div>
          </div>
          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-teal-flow text-on-primary shadow-lg ring-4 ring-teal-flow/30">
            <Icon name="navigation" className="rotate-45 text-[16px]" />
          </div>
        </div>
      </div>

      <div className="absolute bottom-4 left-4 flex items-center gap-3 rounded-xl border border-outline-variant/30 bg-surface-container-lowest/90 px-3 py-1.5 text-[11px] font-medium text-deep-river shadow-sm backdrop-blur-md">
        <span className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-full bg-teal-flow" /> {t('Operational')}
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-full bg-sky-aqua" /> {t('Vessel Tracking')}
        </span>
        <span className="flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-full bg-signal-amber" /> {t('Tide Alert: Clear')}
        </span>
      </div>
    </div>
  )
}
