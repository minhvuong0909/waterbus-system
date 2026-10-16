import { useState } from 'react'
import type { ReactNode } from 'react'
import { Icon } from '../ui'
import type { CorridorMap, Pier } from '../../features/trips/types'
import { t } from '../../i18n'
import PierPopup from './PierPopup'

interface RiverMapProps {
  map: CorridorMap
  className?: string
  /** Buttons shown in the card of the clicked pier. */
  pierActions?: (pier: Pier) => ReactNode
}

const W = 700
const H = 460

type Point = [number, number]

/** Smooth curve through the points (Catmull-Rom turned into cubic Béziers). */
function smoothPath(points: Point[]): string {
  if (points.length < 2) return ''
  const at = (i: number) => points[Math.min(Math.max(i, 0), points.length - 1)]
  let d = `M ${points[0][0]} ${points[0][1]}`
  for (let i = 0; i < points.length - 1; i++) {
    const [p0, p1, p2, p3] = [at(i - 1), at(i), at(i + 1), at(i + 2)]
    d += ` C ${p1[0] + (p2[0] - p0[0]) / 6} ${p1[1] + (p2[1] - p0[1]) / 6}, ${
      p2[0] - (p3[0] - p1[0]) / 6
    } ${p2[1] - (p3[1] - p1[1]) / 6}, ${p2[0]} ${p2[1]}`
  }
  return d
}

/** The river runs through every pier and carries on past both ends, off the canvas. */
function riverPath(piers: Pier[]): string {
  const pts: Point[] = piers.map((p) => [(p.x / 100) * W, (p.y / 100) * H])
  if (pts.length < 2) return ''
  const first = pts[0]
  const second = pts[1]
  const last = pts[pts.length - 1]
  const prev = pts[pts.length - 2]
  const before: Point = [first[0] - (second[0] - first[0]) * 0.7, first[1] - (second[1] - first[1]) * 0.7]
  const after: Point = [last[0] + (last[0] - prev[0]) * 0.7, last[1] + (last[1] - prev[1]) * 0.7]
  return smoothPath([before, ...pts, after])
}

const PIER_STYLES: Record<Pier['kind'], { dot: string; label: string; sub: string }> = {
  hub: {
    dot: 'h-8 w-8 bg-deep-river ring-4 ring-white/80',
    label: 'rounded-xl px-3 py-1.5 text-xs font-bold',
    sub: 'font-semibold text-teal-flow',
  },
  terminus: {
    dot: 'h-8 w-8 bg-deep-river ring-4 ring-white/80',
    label: 'rounded-xl px-3 py-1.5 text-xs font-bold',
    sub: 'font-semibold text-teal-flow',
  },
  stop: {
    dot: 'h-6 w-6 bg-teal-flow ring-4 ring-white/80',
    label: 'rounded-lg px-2.5 py-1 text-xs font-semibold',
    sub: 'text-on-surface-variant',
  },
}

const PIER_ICON: Partial<Record<Pier['kind'], string>> = { hub: 'anchor', terminus: 'flag' }

/** Rooftops of the city on one bank: a few blocks, a stepped tower and a tall mast. */
function Skyline({ className }: { className: string }) {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 260 120"
      preserveAspectRatio="xMinYMax meet"
      className={`pointer-events-none absolute ${className}`}
    >
      <defs>
        <linearGradient id="skyline-fill" x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor="#9FB9C2" />
          <stop offset="100%" stopColor="#C9DCE0" />
        </linearGradient>
      </defs>
      <g fill="url(#skyline-fill)" opacity="0.85">
        <rect x="0" y="78" width="26" height="42" rx="2" />
        <rect x="30" y="58" width="22" height="62" rx="2" />
        <rect x="56" y="86" width="30" height="34" rx="2" />
        <polygon points="92,120 92,44 104,22 116,44 116,120" />
        <rect x="120" y="66" width="24" height="54" rx="2" />
        <polygon points="150,120 150,30 158,30 160,6 162,30 170,30 170,120" />
        <rect x="176" y="72" width="28" height="48" rx="2" />
        <rect x="208" y="90" width="22" height="30" rx="2" />
        <rect x="234" y="70" width="26" height="50" rx="2" />
      </g>
      <g fill="#fff" opacity="0.55">
        <rect x="35" y="66" width="3" height="3" />
        <rect x="42" y="74" width="3" height="3" />
        <rect x="99" y="60" width="3" height="3" />
        <rect x="105" y="72" width="3" height="3" />
        <rect x="155" y="48" width="3" height="3" />
        <rect x="162" y="60" width="3" height="3" />
        <rect x="181" y="84" width="3" height="3" />
      </g>
    </svg>
  )
}

/**
 * Illustrated river corridor: land on both banks with a skyline, a layered river with flowing
 * ripples that runs through every pier, and the vessel gliding on it. Pier nodes are positioned in
 * percent of the canvas, so the map follows however many piers the API returns.
 */
export default function RiverMap({ map, className = '', pierActions }: RiverMapProps) {
  const { piers, vessel } = map
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const selected = piers.find((p) => p.id === selectedId)
  const river = riverPath(piers)

  return (
    <div
      className={`relative flex h-[470px] w-full items-center justify-center overflow-hidden rounded-2xl border border-teal-flow/15 bg-gradient-to-br from-[#F4F9F6] via-[#EAF4F0] to-[#E1EFEA] shadow-inner ${className}`.trim()}
    >
      <svg
        className="absolute inset-0 h-full w-full"
        fill="none"
        preserveAspectRatio="none"
        viewBox={`0 0 ${W} ${H}`}
        aria-hidden="true"
      >
        <defs>
          <linearGradient gradientUnits="userSpaceOnUse" id="river-water" x1="0" x2={W} y1="0" y2={H}>
            <stop offset="0%" stopColor="#A8E3EC" />
            <stop offset="50%" stopColor="#5BBBCB" />
            <stop offset="100%" stopColor="#2D93AA" />
          </linearGradient>
          <pattern height="28" id="land-dots" patternUnits="userSpaceOnUse" width="28">
            <circle cx="2" cy="2" fill="#CFE2DA" r="1.1" />
          </pattern>
        </defs>
        <rect fill="url(#land-dots)" height={H} width={W} />
        {river && (
          <>
            <path d={river} opacity="0.7" stroke="#FFFFFF" strokeLinecap="round" strokeWidth="104" />
            <path d={river} stroke="#BDE7EE" strokeLinecap="round" strokeWidth="88" />
            <path d={river} stroke="url(#river-water)" strokeLinecap="round" strokeWidth="68" />
            <g className="river-flow" stroke="#FFFFFF" strokeLinecap="round" strokeWidth="2.2">
              <path d={river} opacity="0.55" strokeDasharray="14 22" transform="translate(0 -14)" />
              <path d={river} opacity="0.4" strokeDasharray="8 26" transform="translate(0 2)" />
              <path d={river} opacity="0.5" strokeDasharray="18 30" transform="translate(0 16)" />
            </g>
          </>
        )}
      </svg>

      <Skyline className="bottom-0 left-0 h-[32%] w-[34%]" />
      <Skyline className="right-[2%] top-[3%] h-[20%] w-[24%] opacity-60" />

      {piers.map((pier) => {
        const style = PIER_STYLES[pier.kind]
        const icon = PIER_ICON[pier.kind]
        const active = pier.id === selectedId
        return (
          <button
            key={pier.id}
            type="button"
            aria-pressed={active}
            aria-label={pier.name}
            onClick={() => setSelectedId(active ? null : pier.id)}
            className="group absolute z-10 flex -translate-y-1/2 cursor-pointer items-center gap-space-xs text-left"
            style={{ left: `${pier.x}%`, top: `${pier.y}%` }}
          >
            <span className="relative flex">
              {pier.kind !== 'stop' && (
                <span className="pier-ripple absolute inset-0 rounded-full bg-deep-river/30" />
              )}
              <span
                className={`relative flex items-center justify-center rounded-full text-on-primary shadow-lg transition-transform group-hover:scale-110 ${style.dot} ${
                  active ? 'scale-110' : ''
                }`}
              >
                {icon ? (
                  <Icon name={icon} className="text-[16px]" />
                ) : (
                  <span className="h-2 w-2 rounded-full bg-on-primary" />
                )}
              </span>
            </span>
            <span
              className={`border border-outline-variant/30 bg-white/95 shadow-md backdrop-blur-sm ${style.label} ${
                active ? 'ring-2 ring-teal-flow' : ''
              }`}
            >
              <span className="block font-headline-sm text-deep-river">{pier.name}</span>
              <span className={`text-[10px] ${style.sub}`}>{pier.subtitle}</span>
            </span>
          </button>
        )
      })}

      <div
        className="absolute z-20 -translate-x-1/2 -translate-y-1/2"
        style={{ left: `${vessel.x}%`, top: `${vessel.y}%` }}
      >
        <div className="relative">
          <div className="absolute right-full top-1/2 mr-3 flex -translate-y-1/2 items-center gap-2.5 whitespace-nowrap rounded-xl border border-white/20 bg-deep-river px-3 py-2 text-on-primary shadow-xl">
            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-signal-amber text-deep-river">
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
          <div className="boat-bob relative flex h-8 w-8 items-center justify-center rounded-full bg-signal-amber text-deep-river shadow-lg ring-4 ring-white/70">
            <Icon name="navigation" className="rotate-45 text-[17px]" />
          </div>
        </div>
      </div>

      {selected && (
        <PierPopup
          pier={selected}
          onClose={() => setSelectedId(null)}
          actions={pierActions?.(selected)}
        />
      )}
    </div>
  )
}
