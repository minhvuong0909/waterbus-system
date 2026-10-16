import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import type { CorridorMap, Pier } from '../../features/trips/types'
import PierPopup from './PierPopup'

interface PierLeafletMapProps {
  map: CorridorMap
  className?: string
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

/**
 * The corridor on a real street map, with the piers at their coordinates. Clicking a pier opens
 * the same photo panel as on the illustrated map. The line joins the piers in river order, so it
 * is a guide to the route, not the exact course of the river.
 */
export default function PierLeafletMap({ map, className = '', pierActions }: PierLeafletMapProps) {
  const located = useMemo(
    () => map.piers.filter((p) => p.latitude !== undefined && p.longitude !== undefined),
    [map.piers],
  )
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const selected = located.find((p) => p.id === selectedId)
  const line = located.map((p): [number, number] => [p.latitude as number, p.longitude as number])
  const bounds = line.length > 1 ? L.latLngBounds(line).pad(0.12) : undefined

  return (
    <div
      className={`relative h-[470px] w-full overflow-hidden rounded-2xl border border-teal-flow/15 ${className}`.trim()}
    >
      <MapContainer
        center={DEFAULT_CENTER}
        zoom={12}
        bounds={bounds}
        scrollWheelZoom={false}
        className="z-0 h-full w-full"
      >
        <TileLayer
          attribution="Tiles &copy; Esri &mdash; Source: Esri, HERE, Garmin, OpenStreetMap contributors"
          url="https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}"
        />
        <Polyline
          positions={line}
          pathOptions={{ color: '#147A7E', weight: 5, opacity: 0.75, dashArray: '2 10', lineCap: 'round' }}
        />
        {located.map((pier) => (
          <Marker
            key={pier.id}
            position={[pier.latitude as number, pier.longitude as number]}
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
