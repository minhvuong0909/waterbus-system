import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../constant/env'
import { CircleMarker, MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'

export interface MapStation {
  id: string
  name: string
  latitude: number
  longitude: number
}

export interface MapBoat {
  tripId: string
  code: string
  latitude: number
  longitude: number
}

interface FleetMapProps {
  /** Stations in river order; the line of the corridor is drawn through them. */
  stations: MapStation[]
  boats: MapBoat[]
  /**
   * Course of the route along the river. Without it the line joins the stations with straight
   * segments, which cut across land where the river bends.
   */
  path?: [number, number][]
  className?: string
}

/** Boat icon: a teal disc with a boat glyph, so no image files are needed. */
const boatIcon = L.divIcon({
  className: 'fleet-boat',
  html: '<span class="material-symbols-outlined">directions_boat</span>',
  iconSize: [34, 34],
  iconAnchor: [17, 17],
})

// Used until there are stations to fit the view to: the middle of the Saigon River corridor.
const DEFAULT_CENTER: [number, number] = [10.81, 106.73]

/** OpenStreetMap view of the river corridor with its stations and the boats on it. */
export default function FleetMap({ stations, boats, path, className = '' }: FleetMapProps) {
  const stationPoints = stations.map((s): [number, number] => [s.latitude, s.longitude])
  const line = path && path.length > 1 ? path : stationPoints
  const bounds = line.length > 1 ? L.latLngBounds(line).pad(0.2) : undefined

  return (
    <MapContainer
      center={DEFAULT_CENTER}
      zoom={12}
      bounds={bounds}
      scrollWheelZoom
      className={`z-0 h-full min-h-[24rem] w-full rounded-xl ${className}`.trim()}
    >
      <TileLayer attribution={MAP_TILE_ATTRIBUTION} url={MAP_TILE_URL} />
      <Polyline positions={line} pathOptions={{ color: '#147A7E', weight: 4, opacity: 0.8 }} />
      {stations.map((s) => (
        <CircleMarker
          key={s.id}
          center={[s.latitude, s.longitude]}
          radius={7}
          pathOptions={{ color: '#0D2538', weight: 3, fillColor: '#fff', fillOpacity: 1 }}
        >
          <Tooltip direction="top" offset={[0, -6]}>
            {s.name}
          </Tooltip>
        </CircleMarker>
      ))}
      {boats.map((b) => (
        <Marker key={b.tripId} position={[b.latitude, b.longitude]} icon={boatIcon}>
          <Tooltip direction="top" offset={[0, -14]}>
            {b.code}
          </Tooltip>
        </Marker>
      ))}
    </MapContainer>
  )
}
