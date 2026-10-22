import { Icon } from '../ui'
import type { TrackingPoi } from '../../features/tracking/types'
import { t } from '../../i18n'

// Pieces of the stylised live map (LiveTripMap): the zoom controls and one point-of-interest pin.

export function MapControls({
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

export function PoiPin({
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
