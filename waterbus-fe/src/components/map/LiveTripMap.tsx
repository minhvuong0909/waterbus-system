import { useState } from 'react'
import { Icon } from '../ui'
import type { TrackingMode, TrackingPoi } from '../../features/tracking/types'
import { t } from '../../i18n'

interface LiveTripMapProps {
  pois: TrackingPoi[]
  activePoiId: string
  mode: TrackingMode
  remainingDetail: string
  onSelectPoi: (id: string) => void
}

// Vessel and route waypoints on the 920x660 canvas.
const VESSEL = { x: 510, y: 322 }
const ORIGIN = { x: 248, y: 280 }
const DESTINATION = { x: 672, y: 350 }

function MapControls({
  zoom,
  onZoom,
  onRecenter,
}: {
  zoom: number
  onZoom: (delta: number) => void
  onRecenter: () => void
}) {
  const btn =
    'flex h-8 w-8 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container hover:text-deep-river disabled:opacity-40'
  return (
    <div className="pointer-events-auto flex items-center gap-space-xs">
      <div className="flex flex-col rounded-xl border border-outline-variant/40 bg-white/95 p-1 shadow-sm backdrop-blur-md">
        <button
          type="button"
          aria-label={t('Zoom in')}
          disabled={zoom >= 1.6}
          onClick={() => onZoom(0.2)}
          className={btn}
        >
          <Icon name="add" className="text-[18px]" />
        </button>
        <button
          type="button"
          aria-label={t('Zoom out')}
          disabled={zoom <= 1}
          onClick={() => onZoom(-0.2)}
          className={btn}
        >
          <Icon name="remove" className="text-[18px]" />
        </button>
      </div>
      <button
        type="button"
        aria-label={t('Recenter view')}
        onClick={onRecenter}
        className="flex h-10 w-10 items-center justify-center rounded-xl border border-outline-variant/40 bg-white/95 text-teal-flow shadow-sm backdrop-blur-md hover:text-deep-river"
      >
        <Icon name="my_location" className="text-[20px]" />
      </button>
    </div>
  )
}

function PoiPin({
  poi,
  active,
  onSelect,
}: {
  poi: TrackingPoi
  active: boolean
  onSelect: (id: string) => void
}) {
  const passed = poi.state === 'passed'
  const dot = passed ? '#73777D' : active ? '#F08A6B' : '#4FC3D8'

  return (
    <g
      transform={`translate(${poi.x}, ${poi.y})`}
      className="cursor-pointer"
      role="button"
      tabIndex={0}
      aria-label={`${poi.mapTitle}: ${poi.mapNote}`}
      onClick={() => onSelect(poi.id)}
      onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && onSelect(poi.id)}
    >
      {active && (
        <>
          <circle
            r="38"
            fill="url(#poiPulseWarm)"
            className="animate-ping"
            style={{ animationDuration: '2.2s' }}
          />
          <circle r="24" fill="#F08A6B" opacity="0.22" />
        </>
      )}
      <circle
        r={active ? 14 : 12}
        fill={active ? '#0D2538' : passed ? '#E3E2E4' : '#fff'}
        stroke={active ? '#F08A6B' : dot}
        strokeWidth={active ? 3 : 2.2}
      />
      <circle r={active ? 5 : 4} fill={active ? '#F08A6B' : dot} />
      {active ? (
        <g transform="translate(-95, -72)">
          <rect fill="#0D2538" height="56" rx="12" width="190" />
          <polygon fill="#0D2538" points="95,56 88,64 102,64" />
          <circle cx="18" cy="18" r="4" fill="#F08A6B" />
          <text
            fill="#F08A6B"
            fontFamily="Plus Jakarta Sans"
            fontSize="9"
            fontWeight="800"
            letterSpacing="0.6"
            x="28"
            y="21"
          >
            {t('NOW APPROACHING • ACTIVE STORY')}
          </text>
          <text
            fill="#fff"
            fontFamily="Plus Jakarta Sans"
            fontSize="11.5"
            fontWeight="700"
            x="18"
            y="37"
          >
            {poi.mapTitle}
          </text>
          <text fill="#CEEFF4" fontFamily="Inter" fontSize="9" fontWeight="500" x="18" y="49">
            {poi.mapNote}
          </text>
        </g>
      ) : (
        <g transform={passed ? 'translate(-155, -20)' : 'translate(18, -20)'}>
          <rect
            fill="#fff"
            height="40"
            rx="8"
            stroke="#CBD5E1"
            width={passed ? 145 : 164}
            opacity="0.95"
          />
          <text
            fill={passed ? '#43474C' : '#0D2538'}
            fontFamily="Plus Jakarta Sans"
            fontSize="10.5"
            fontWeight="700"
            x="12"
            y="17"
          >
            {poi.mapTitle}
          </text>
          <text
            fill={passed ? '#73777D' : '#147A7E'}
            fontFamily="Inter"
            fontSize="9"
            fontWeight="600"
            x="12"
            y="30"
          >
            {poi.mapNote}
          </text>
        </g>
      )}
    </g>
  )
}

/** Stylised live map of the central corridor: river, route progress, POIs and the vessel. */
export default function LiveTripMap({
  pois,
  activePoiId,
  mode,
  remainingDetail,
  onSelectPoi,
}: LiveTripMapProps) {
  const [zoom, setZoom] = useState(1)
  const sightseeing = mode === 'sightseeing'
  const visiblePois = sightseeing ? pois : []

  return (
    <div className="relative flex min-h-[660px] flex-col overflow-hidden rounded-panel border border-outline-variant/50 bg-white shadow-[0_4px_24px_rgba(13,37,56,0.06)]">
      <div className="pointer-events-none absolute inset-x-space-md top-space-md z-20 flex items-center justify-between">
        <div className="pointer-events-auto flex items-center gap-space-sm">
          <div className="flex items-center gap-2 rounded-full border border-outline-variant/40 bg-white/95 px-space-md py-1.5 shadow-sm backdrop-blur-md">
            <span className="h-2.5 w-2.5 animate-pulse rounded-full bg-teal-flow" />
            <span className="font-headline-sm text-body-sm font-bold text-deep-river">
              {t('Saigon River Central Corridor')}
            </span>
          </div>
          <div className="hidden items-center gap-1.5 rounded-full bg-deep-river px-3 py-1.5 font-headline-sm text-body-sm font-semibold text-white shadow-sm sm:flex">
            <Icon name={sightseeing ? 'tour' : 'commute'} className="text-[16px] text-coral-glow" />
            {sightseeing ? t('Sightseeing Layer Active') : t('Regular Transit View')}
          </div>
        </div>
        <MapControls
          zoom={zoom}
          onZoom={(d) => setZoom((z) => Math.min(1.6, Math.max(1, +(z + d).toFixed(2))))}
          onRecenter={() => setZoom(1)}
        />
      </div>

      <div className="relative h-[660px] w-full select-none overflow-hidden bg-[#E8F2EF]">
        <svg
          className="h-full w-full origin-center transition-transform duration-300"
          style={{ transform: `scale(${zoom})` }}
          fill="none"
          viewBox="0 0 920 660"
          preserveAspectRatio="xMidYMid slice"
          role="img"
          aria-label={t('Live map of the river crossing')}
        >
          <defs>
            <linearGradient id="riverDepth" x1="0%" y1="30%" x2="100%" y2="70%">
              <stop offset="0%" stopColor="#D7F3F6" />
              <stop offset="35%" stopColor="#C0ECF3" />
              <stop offset="65%" stopColor="#A2E2ED" />
              <stop offset="100%" stopColor="#8CD7E4" />
            </linearGradient>
            <radialGradient id="poiPulseWarm">
              <stop offset="0%" stopColor="#F08A6B" stopOpacity="0.7" />
              <stop offset="50%" stopColor="#E8B63E" stopOpacity="0.3" />
              <stop offset="100%" stopColor="#F08A6B" stopOpacity="0" />
            </radialGradient>
          </defs>

          <path
            d="M0 0 H335 C308 125 268 208 246 308 C220 422 284 532 296 660 H0 V0 Z"
            fill="#F4F7F8"
          />
          <path
            d="M920 0 H588 C610 112 662 222 682 332 C702 442 642 552 622 660 H920 V0 Z"
            fill="#EEF5F2"
          />
          <path
            d="M335 0 C308 125 268 208 246 308 C220 422 284 532 296 660 H622 C642 552 702 442 682 332 C662 222 610 112 588 0 Z"
            fill="url(#riverDepth)"
          />
          <g stroke="#fff" opacity="0.55" strokeLinecap="round" fill="none">
            <path
              d="M385 60 C365 140 338 220 328 300 C316 400 366 500 380 600"
              strokeWidth="2"
              strokeDasharray="24 20"
            />
            <path
              d="M445 40 C420 130 405 230 415 320 C425 410 470 510 488 610"
              strokeWidth="2.5"
              strokeDasharray="36 24"
            />
            <path
              d="M510 50 C490 145 482 235 496 325 C512 415 548 515 554 620"
              strokeWidth="2"
              strokeDasharray="28 22"
            />
          </g>
          <g opacity="0.28" fill="#0D2538">
            <rect x="40" y="90" width="48" height="38" rx="3" />
            <rect x="96" y="82" width="36" height="46" rx="3" />
            <rect x="30" y="180" width="62" height="42" rx="3" />
            <rect x="100" y="172" width="50" height="50" rx="3" />
            <rect x="35" y="290" width="52" height="45" rx="3" />
            <rect x="45" y="390" width="65" height="38" rx="3" />
            <rect x="30" y="490" width="75" height="44" rx="3" />
          </g>
          <g opacity="0.35" fill="#D2E7DC">
            <circle cx="780" cy="220" r="90" />
            <circle cx="750" cy="520" r="70" />
          </g>
          <path
            d="M335 0 C308 125 268 208 246 308 C220 422 284 532 296 660"
            stroke="#CBD5E1"
            strokeWidth="6"
            opacity="0.7"
          />
          <path
            d="M588 0 C610 112 662 222 682 332 C702 442 642 552 622 660"
            stroke="#CBD5E1"
            strokeWidth="6"
            opacity="0.7"
          />

          <g opacity="0.6">
            <path
              d="M 292 90 C 430 110, 500 120, 608 145"
              stroke="#fff"
              strokeWidth="7"
              strokeLinecap="round"
            />
            <path
              d="M 292 90 C 430 110, 500 120, 608 145"
              stroke="#475569"
              strokeWidth="3.5"
              strokeLinecap="round"
            />
            <rect x="395" y="78" width="86" height="18" rx="4" fill="#fff" opacity="0.9" />
            <text
              x="438"
              y="90"
              textAnchor="middle"
              fill="#43474C"
              fontFamily="Plus Jakarta Sans"
              fontSize="8.5"
              fontWeight="700"
              letterSpacing="0.3"
            >
              {t('BA SON BRIDGE')}
            </text>
          </g>

          <g transform="translate(36, 42)">
            <rect fill="#fff" height="36" rx="8" width="215" opacity="0.94" stroke="#CBD5E1" />
            <circle cx="16" cy="18" r="4" fill="#147A7E" />
            <text
              fill="#0D2538"
              fontFamily="Plus Jakarta Sans"
              fontSize="10.5"
              fontWeight="800"
              letterSpacing="0.5"
              x="28"
              y="16"
            >
              {t('DISTRICT 1 (D1)')}
            </text>
            <text fill="#43474C" fontFamily="Inter" fontSize="9" fontWeight="500" x="28" y="28">
              {t('HISTORIC RIVERFRONT')}
            </text>
          </g>
          <g transform="translate(674, 42)">
            <rect fill="#fff" height="36" rx="8" width="220" opacity="0.94" stroke="#CBD5E1" />
            <circle cx="16" cy="18" r="4" fill="#4FC3D8" />
            <text
              fill="#0D2538"
              fontFamily="Plus Jakarta Sans"
              fontSize="10.5"
              fontWeight="800"
              letterSpacing="0.5"
              x="28"
              y="16"
            >
              {t('THU THIEM (D2)')}
            </text>
            <text fill="#43474C" fontFamily="Inter" fontSize="9" fontWeight="500" x="28" y="28">
              {t('PROMENADE & FINANCIAL CORRIDOR')}
            </text>
          </g>

          {/* Completed and remaining track */}
          <path
            d={`M ${ORIGIN.x} ${ORIGIN.y} C 330 280, 420 302, ${VESSEL.x} ${VESSEL.y}`}
            stroke="#147A7E"
            strokeWidth="5.5"
            strokeLinecap="round"
          />
          <path
            d={`M ${ORIGIN.x} ${ORIGIN.y} C 330 280, 420 302, ${VESSEL.x} ${VESSEL.y}`}
            stroke="#9BF1F5"
            strokeWidth="1.8"
            strokeLinecap="round"
            opacity="0.8"
          />
          <path
            d={`M ${VESSEL.x} ${VESSEL.y} C 565 334, 620 344, ${DESTINATION.x} ${DESTINATION.y}`}
            stroke="#4FC3D8"
            strokeWidth="4"
            strokeDasharray="8 6"
            strokeLinecap="round"
          />

          {visiblePois.map((poi) => (
            <PoiPin key={poi.id} poi={poi} active={poi.id === activePoiId} onSelect={onSelectPoi} />
          ))}

          {/* Terminal piers */}
          <g transform={`translate(${ORIGIN.x}, ${ORIGIN.y})`}>
            <circle r="15" fill="#147A7E" />
            <circle r="6.5" fill="#fff" />
            <g transform="translate(-165, -58)">
              <rect fill="#fff" height="52" rx="10" width="154" stroke="#CBD5E1" />
              <text
                fill="#0D2538"
                fontFamily="Plus Jakarta Sans"
                fontSize="12"
                fontWeight="800"
                x="16"
                y="21"
              >
                {t('Bach Dang Pier')}
              </text>
              <text
                fill="#147A7E"
                fontFamily="Inter"
                fontSize="10.5"
                fontWeight="600"
                x="16"
                y="37"
              >
                {t('Departed 08:30')}
              </text>
            </g>
          </g>
          <g transform={`translate(${DESTINATION.x}, ${DESTINATION.y})`}>
            <circle r="16" fill="#fff" stroke="#147A7E" strokeWidth="2.5" />
            <circle r="8" fill="#4FC3D8" />
            <circle r="3.5" fill="#fff" />
            <g transform="translate(20, -28)">
              <rect fill="#fff" height="52" rx="10" width="158" stroke="#CBD5E1" />
              <text
                fill="#0D2538"
                fontFamily="Plus Jakarta Sans"
                fontSize="12"
                fontWeight="800"
                x="16"
                y="21"
              >
                {t('Thu Thiem Pier')}
              </text>
              <text fill="#43474C" fontFamily="Inter" fontSize="10.5" x="16" y="37">
                {t('Scheduled 08:42')}
              </text>
            </g>
          </g>

          {/* Live vessel */}
          <g transform={`translate(${VESSEL.x}, ${VESSEL.y})`}>
            <circle
              r="36"
              fill="#4FC3D8"
              opacity="0.2"
              className="animate-ping"
              style={{ animationDuration: '2.6s' }}
            />
            <circle r="24" fill="#147A7E" opacity="0.18" />
            <g transform="rotate(14)">
              <path
                d="M -24 -9 L -52 -18 M -24 9 L -52 18"
                stroke="#fff"
                strokeWidth="2.2"
                strokeLinecap="round"
                opacity="0.8"
              />
              <path
                d="M -22 -12 C -10 -14, 14 -12, 24 -7 C 14 -3, -10 -4, -22 -6 Z"
                fill="#0D2538"
              />
              <path d="M -22 6 C -10 4, 14 3, 24 7 C 14 12, -10 14, -22 12 Z" fill="#0D2538" />
              <rect x="-16" y="-6" width="30" height="12" rx="3" fill="#147A7E" />
              <path d="M -6 -4.5 L 8 -3.5 C 11 -1.5, 11 1.5, 8 3.5 L -6 4.5 Z" fill="#D7F3F6" />
              <circle cx="14" cy="0" r="2.2" fill="#4FC3D8" />
            </g>
            <g transform="translate(-68, -68)">
              <rect fill="#0D2538" height="44" rx="10" width="136" />
              <polygon fill="#0D2538" points="68,44 61,51 75,51" />
              <circle cx="18" cy="22" r="4.5" fill="#4FC3D8" />
              <text
                fill="#fff"
                fontFamily="Plus Jakarta Sans"
                fontSize="11"
                fontWeight="800"
                x="28"
                y="19"
              >
                {t('WB-01 • En Route')}
              </text>
              <text fill="#9BF1F5" fontFamily="Inter" fontSize="9.5" fontWeight="600" x="28" y="32">
                {t('Mid-Crossing')}
              </text>
            </g>
          </g>
        </svg>

        <div className="pointer-events-none absolute inset-x-space-md bottom-space-md">
          <div className="pointer-events-auto flex flex-wrap items-center justify-between gap-space-md rounded-2xl border border-outline-variant/40 bg-white/95 p-space-md shadow-md backdrop-blur-md">
            <div className="flex items-center gap-space-md">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-teal-flow/10 text-teal-flow">
                <Icon name="nature_people" className="text-[22px]" />
              </div>
              <div>
                <div className="font-headline-sm text-body-sm font-bold text-deep-river">
                  {t('Saigon Scenic River Corridor')}
                </div>
                <div className="text-body-sm text-on-surface-variant">
                  {t('Estimated remaining crossing:')}{' '}
                  <strong className="font-semibold text-deep-river">{remainingDetail}</strong>
                </div>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-space-md text-body-sm font-medium text-on-surface-variant">
              <span className="flex items-center gap-1.5">
                <span className="h-1.5 w-3.5 rounded-full bg-teal-flow" />
                {t('Completed track')}
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-1.5 w-3.5 rounded-full border-t-2 border-dashed border-sky-aqua" />
                {t('Remaining track')}
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-coral-glow" />
                {t('Active Audio POI')}
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2.5 w-2.5 rounded-full bg-deep-river" />
                {t('Terminal Pier')}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
