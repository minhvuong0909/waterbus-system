import { Icon } from '../ui'
import { t } from '../../i18n'

interface NetworkPin {
  x: number
  y: number
  label: string
  sub?: string
  kind: 'primary' | 'stop' | 'minor' | 'terminus'
}

// Pin positions are in the 740x440 SVG canvas.
const PINS: NetworkPin[] = [
  { x: 130, y: 365, label: 'Bach Dang (D1)', sub: 'PRIMARY TERMINAL', kind: 'primary' },
  { x: 260, y: 290, label: 'Thu Thiem Pier', kind: 'stop' },
  { x: 385, y: 220, label: 'Van Thanh', kind: 'minor' },
  { x: 495, y: 155, label: 'Binh An Pier', kind: 'stop' },
  { x: 640, y: 95, label: 'Linh Dong Hub', kind: 'terminus' },
]

const RIVER = 'M 10 400 C 140 375 220 325 290 280 C 375 225 445 175 540 135 C 620 100 680 85 730 70'

function Pin({ pin }: { pin: NetworkPin }) {
  const { x, y, kind } = pin
  const label = t(pin.label)
  const sub = pin.sub ? t(pin.sub) : undefined
  const dark = kind === 'primary' || kind === 'terminus'
  return (
    <g transform={`translate(${x}, ${y})`}>
      <circle
        r={dark ? 22 : kind === 'minor' ? 14 : 18}
        fill={kind === 'minor' ? '#0D2538' : '#147A7E'}
        opacity={kind === 'minor' ? 0.12 : 0.15}
      />
      <circle
        r={kind === 'primary' ? 14 : kind === 'terminus' ? 12 : kind === 'minor' ? 8 : 10}
        fill="#fff"
        stroke={dark || kind === 'minor' ? '#0D2538' : '#147A7E'}
        strokeWidth={kind === 'minor' ? 2.5 : 3}
      />
      <circle
        r={kind === 'primary' ? 6 : kind === 'terminus' ? 5 : kind === 'minor' ? 3.5 : 4.5}
        fill={dark || kind === 'minor' ? '#0D2538' : '#147A7E'}
      />
      {dark ? (
        <>
          <rect
            fill="#0D2538"
            height={kind === 'primary' ? 28 : 26}
            rx="7"
            width={kind === 'primary' ? 130 : 110}
            x={kind === 'primary' ? -65 : -55}
            y={kind === 'primary' ? -48 : -36}
          />
          <text
            fill="#fff"
            fontFamily="Plus Jakarta Sans"
            fontSize="11"
            fontWeight="700"
            textAnchor="middle"
            y={kind === 'primary' ? -30 : -19}
          >
            {label}
          </text>
          {sub && (
            <text
              fill="#147A7E"
              fontFamily="Inter"
              fontSize="9"
              fontWeight="700"
              textAnchor="middle"
              y="-12"
            >
              {sub}
            </text>
          )}
        </>
      ) : kind === 'minor' ? (
        <>
          <rect fill="#fff" height="22" rx="6" stroke="#E2ECEE" width="76" x="-85" y="-14" />
          <text
            fill="#43474C"
            fontFamily="Plus Jakarta Sans"
            fontSize="10"
            fontWeight="600"
            textAnchor="middle"
            x="-47"
            y="2"
          >
            {label}
          </text>
        </>
      ) : (
        <>
          <rect
            fill="#fff"
            height="26"
            rx="6"
            stroke="#E2ECEE"
            strokeWidth="1.5"
            width={label.length * 7 + 16}
            x="18"
            y="-16"
          />
          <text
            fill="#0D2538"
            fontFamily="Plus Jakarta Sans"
            fontSize="11"
            fontWeight="700"
            x="26"
            y="2"
          >
            {label}
          </text>
        </>
      )}
    </g>
  )
}

/** Diagonal river-network map used on the search page: line 1 corridor, 5 piers, live vessel. */
export default function NetworkMap() {
  return (
    <div className="relative flex min-h-[440px] flex-col justify-between overflow-hidden rounded-2xl border border-teal-flow/20 bg-gradient-to-b from-[#E0F2F5] via-[#EBF7F8] to-[#E3EFF3] p-5 shadow-inner sm:p-6">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(#147a7e_1px,transparent_1px)] opacity-40 [background-size:18px_18px]" />
      <div className="pointer-events-none absolute inset-x-0 top-3 flex justify-between px-8 text-teal-flow opacity-25">
        <Icon name="waves" className="text-[24px]" />
        <Icon name="water" className="text-[24px]" />
        <Icon name="waves" className="text-[24px]" />
      </div>
      <div className="pointer-events-none absolute inset-x-0 bottom-4 flex justify-between px-10 text-teal-flow opacity-25">
        <Icon name="water" className="text-[26px]" />
        <Icon name="waves" className="text-[26px]" />
        <Icon name="water" className="text-[26px]" />
      </div>

      <svg
        className="pointer-events-none absolute inset-0 h-full w-full"
        preserveAspectRatio="none"
        viewBox="0 0 740 440"
        aria-hidden="true"
      >
        <defs>
          <linearGradient id="riverGradient" x1="0%" x2="100%" y1="100%" y2="0%">
            <stop offset="0%" stopColor="#4FC3D8" stopOpacity="0.28" />
            <stop offset="50%" stopColor="#147A7E" stopOpacity="0.35" />
            <stop offset="100%" stopColor="#4FC3D8" stopOpacity="0.3" />
          </linearGradient>
          <linearGradient id="waterFlowLine" x1="0%" x2="100%" y1="100%" y2="0%">
            <stop offset="0%" stopColor="#147A7E" />
            <stop offset="50%" stopColor="#00696d" />
            <stop offset="100%" stopColor="#147A7E" />
          </linearGradient>
        </defs>
        <path
          d="M 0 380 C 130 360 210 320 280 270 C 370 205 440 160 540 120 C 620 90 680 75 740 60 L 740 140 C 660 155 600 170 520 200 C 420 240 350 290 260 350 C 180 400 100 425 0 440 Z"
          fill="url(#riverGradient)"
          opacity="0.8"
        />
        <path
          d={RIVER}
          fill="none"
          opacity="0.45"
          stroke="#4FC3D8"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth="26"
        />
        <path
          d={RIVER}
          fill="none"
          stroke="url(#waterFlowLine)"
          strokeDasharray="10 8"
          strokeLinecap="round"
          strokeWidth="4"
        />
        <text
          fill="#0D2538"
          fontFamily="Plus Jakarta Sans"
          fontSize="11"
          fontWeight="700"
          letterSpacing="1.5"
          opacity="0.45"
          x="70"
          y="425"
        >
          {t('DISTRICT 1 WATERFRONT')}
        </text>
        <text
          fill="#0D2538"
          fontFamily="Plus Jakarta Sans"
          fontSize="11"
          fontWeight="700"
          letterSpacing="1.5"
          opacity="0.45"
          x="280"
          y="340"
        >
          {t('THU THIEM PENINSULA')}
        </text>
        {PINS.map((pin) => (
          <Pin key={pin.label} pin={pin} />
        ))}
        <g transform="translate(205, 325)">
          <circle r="24" fill="#4FC3D8" opacity="0.25">
            <animate attributeName="r" dur="2.5s" repeatCount="indefinite" values="12;28;12" />
            <animate
              attributeName="opacity"
              dur="2.5s"
              repeatCount="indefinite"
              values="0.4;0.05;0.4"
            />
          </circle>
          <circle r="12" fill="#147A7E" opacity="0.3" />
          <circle r="7" fill="#147A7E" stroke="#fff" strokeWidth="2.5" />
        </g>
      </svg>

      <div className="relative z-10 flex items-center justify-between gap-2">
        <div className="inline-flex items-center gap-2 rounded-full border border-teal-flow/20 bg-white/95 px-3.5 py-1.5 font-headline-sm text-xs font-semibold text-deep-river backdrop-blur-md">
          <Icon name="alt_route" className="text-[16px] text-teal-flow" />
          <span>{t('Line 1 Express Corridor • 10.8 km Navigable Waterway')}</span>
        </div>
        <span className="hidden items-center gap-1.5 rounded-full border border-teal-flow/20 bg-[#EBF2F0] px-3 py-1 text-[11px] font-bold text-teal-flow sm:inline-flex">
          <span className="h-1.5 w-1.5 animate-ping rounded-full bg-teal-flow" />
          {t('GPS Live Fleet Radar')}
        </span>
      </div>

      <div className="relative z-10 flex items-center gap-3 self-start rounded-xl border border-teal-flow/30 bg-white/95 p-3 shadow-md backdrop-blur-md sm:px-4 sm:py-2.5">
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border border-teal-flow/20 bg-sand-light text-teal-flow">
          <Icon name="directions_boat" className="text-[20px]" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <p className="font-headline-sm text-xs font-bold text-deep-river">
              {t('WB-01 • In Transit')}
            </p>
            <span className="rounded bg-secondary-container px-1.5 py-0.5 text-[10px] font-bold text-on-secondary-container">
              {t('42 km/h')}
            </span>
          </div>
          <p className="text-[11px] font-medium text-on-surface-variant">
            {t('En route to Thu Thiem Pier • ETA 2 min')}
          </p>
        </div>
      </div>
    </div>
  )
}
