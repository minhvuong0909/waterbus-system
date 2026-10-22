import { useState } from 'react'
import { Icon } from '../ui'
import CorridorBackdrop from './CorridorBackdrop'
import { MapControls, PoiPin } from './LiveTripMapParts'
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
          role="group"
          aria-label={t('Live map of the river crossing')}
        >
          <CorridorBackdrop />

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
                {t('Linh Dong Pier')}
              </text>
              <text fill="#43474C" fontFamily="Inter" fontSize="10.5" x="16" y="37">
                {t('Scheduled 09:15')}
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
                {t('SWB-01 • En Route')}
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
