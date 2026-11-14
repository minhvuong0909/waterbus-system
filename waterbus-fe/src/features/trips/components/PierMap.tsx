import { useEffect, useMemo } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap } from 'react-leaflet'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../../constant/env'
import type { CorridorMap } from '../types'

interface PierMapProps {
  map: CorridorMap
  /** The pier shown as chosen; its pin is larger and dark. */
  selectedId?: string
  onSelect?: (pierId: string) => void
  className?: string
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
 * The corridor on a street map: the piers at their coordinates and the line joining them in river
 * order (a guide to the route, not the exact course of the river). Clicking a pier selects it; the
 * page shows its details next to the map.
 */
export default function PierMap({ map, selectedId, onSelect, className = '' }: PierMapProps) {
  const line = useMemo(
    () => map.piers.map((p): [number, number] => [p.latitude, p.longitude]),
    [map.piers],
  )

  return (
    <div className={`relative w-full overflow-hidden rounded-2xl border border-teal-flow/15 ${className}`.trim()}>
      <MapContainer center={DEFAULT_CENTER} zoom={12} scrollWheelZoom={false} className="z-0 h-full w-full">
        <FitToPiers points={line} />
        <TileLayer attribution={MAP_TILE_ATTRIBUTION} url={MAP_TILE_URL} />
        <Polyline
          positions={line}
          pathOptions={{ color: '#147A7E', weight: 5, opacity: 0.75, dashArray: '2 10', lineCap: 'round' }}
        />
        {map.piers.map((pier) => (
          <Marker
            key={pier.id}
            position={[pier.latitude, pier.longitude]}
            icon={pierIcon(pier.id === selectedId)}
            zIndexOffset={pier.id === selectedId ? 1000 : 0}
            eventHandlers={{ click: () => onSelect?.(pier.id) }}
          >
            <Tooltip direction="top" offset={[0, -16]}>
              {pier.name}
            </Tooltip>
          </Marker>
        ))}
      </MapContainer>
    </div>
  )
}
