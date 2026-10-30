import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap } from 'react-leaflet'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../constant/env'
import type { CorridorMap, Pier } from '../../features/trips/types'
import PierPopup from './PierPopup'

interface PierMapProps {
  map: CorridorMap
  className?: string
  compact?: boolean
  /** Buttons shown in the card of the clicked pier. */
  pierActions?: (pier: Pier) => ReactNode
}

// Used until there are piers to fit the view to: the middle of the Saigon River corridor.
const DEFAULT_CENTER: [number, number] = [10.81, 106.73]

/** Pin for a pier: a disc with an anchor, larger and dark when it is the selected one. */
function pierIcon(selected: boolean): L.DivIcon {
  const size = selected ? 40 : 32
  return L.divIcon({
    className: 'pier-pin',
    html: `<span class="pier-pin__disc${
      selected ? ' pier-pin__disc--active' : ''
    }"><span class="material-symbols-outlined">anchor</span></span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  })
}

/** Fits the view to the piers once the map has its real size, and again if the piers change. */
function FitToPiers({ points }: { points: [number, number][] }) {
  const map = useMap()
  useEffect(() => {
    map.invalidateSize()
    if (points.length > 1) map.fitBounds(L.latLngBounds(points), { padding: [48, 48], maxZoom: 15 })
    else if (points.length === 1) map.setView(points[0], 14)
  }, [map, points])
  return null
}

/**
 * The corridor on a street map: the piers at their coordinates, the line joining them in river
 * order (a guide to the route, not the exact course of the river) and the vessel. Clicking a pier
 * opens a panel with its photos and the actions the page offers.
 */
export default function PierMap({
  map,
  className = '',
  compact = false,
  pierActions,
}: PierMapProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const selected = map.piers.find((p) => p.id === selectedId)
  const line = useMemo(
    () => map.piers.map((p): [number, number] => [p.latitude, p.longitude]),
    [map.piers],
  )

  return (
    <div
      className={`relative ${
        compact ? 'h-[320px] sm:h-[380px]' : 'h-[470px]'
      } w-full overflow-hidden rounded-2xl border border-teal-flow/15 ${className}`.trim()}
    >
      <MapContainer
        center={DEFAULT_CENTER}
        zoom={12}
        scrollWheelZoom={false}
        className="z-0 h-full w-full"
      >
        <FitToPiers points={line} />
        <TileLayer attribution={MAP_TILE_ATTRIBUTION} url={MAP_TILE_URL} />
        <Polyline
          positions={line}
          pathOptions={{
            color: '#147A7E',
            weight: 5,
            opacity: 0.75,
            dashArray: '2 10',
            lineCap: 'round',
          }}
        />
        {map.piers.map((pier) => (
          <Marker
            key={pier.id}
            position={[pier.latitude, pier.longitude]}
            icon={pierIcon(pier.id === selectedId)}
            eventHandlers={{ click: () => setSelectedId(pier.id) }}
          >
            <Tooltip direction="top" offset={[0, -16]}>
              {pier.name}
            </Tooltip>
          </Marker>
        ))}
      </MapContainer>

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
