import { useEffect, useMemo } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { CircleMarker, MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap } from 'react-leaflet'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../../constant/env'
import { t } from '../../../i18n'
import type { MapStation } from '../../../components/map/FleetMap'
import type { NarrationPoi } from '../audioGuide'

interface PoiMapProps {
  pois: NarrationPoi[]
  /** Piers in river order, drawn as the corridor so the admin sees where the landmarks sit on the route. */
  stations: MapStation[]
  selectedId: string | null
  /** Landmarks that already have a generated narration in this session. */
  readyIds: ReadonlySet<string>
  onSelect: (id: string) => void
}

/** Pin for a landmark: the pier pin style with a camera glyph, a check when its narration is ready. */
function poiIcon(selected: boolean, ready: boolean): L.DivIcon {
  const size = selected ? 40 : 32
  return L.divIcon({
    className: 'pier-pin',
    html: `<span class="pier-pin__disc${selected ? ' pier-pin__disc--active' : ''}"><span class="material-symbols-outlined">${
      ready ? 'check_circle' : 'photo_camera'
    }</span></span>`,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
  })
}

/** Fits the view to the landmarks once the map has its real size. */
function FitTo({ points }: { points: [number, number][] }) {
  const map = useMap()
  useEffect(() => {
    map.invalidateSize()
    if (points.length > 1) map.fitBounds(L.latLngBounds(points), { padding: [40, 40], maxZoom: 15 })
  }, [map, points])
  return null
}

/** Street map of the landmarks along the river; clicking one selects it for its narration. */
export default function PoiMap({ pois, stations, selectedId, readyIds, onSelect }: PoiMapProps) {
  const poiPoints = useMemo(() => pois.map((p): [number, number] => [p.latitude, p.longitude]), [pois])
  const corridor = stations.map((s): [number, number] => [s.latitude, s.longitude])

  return (
    <div className="h-[420px] w-full overflow-hidden rounded-2xl border border-teal-flow/15 lg:h-[560px]">
      <MapContainer center={[10.785, 106.712]} zoom={14} scrollWheelZoom className="z-0 h-full w-full">
        <FitTo points={poiPoints} />
        <TileLayer attribution={MAP_TILE_ATTRIBUTION} url={MAP_TILE_URL} />
        {corridor.length > 1 && (
          <Polyline positions={corridor} pathOptions={{ color: '#147A7E', weight: 4, opacity: 0.6, dashArray: '2 10' }} />
        )}
        {stations.map((s) => (
          <CircleMarker
            key={s.id}
            center={[s.latitude, s.longitude]}
            radius={6}
            pathOptions={{ color: '#0D2538', weight: 3, fillColor: '#fff', fillOpacity: 1 }}
          >
            <Tooltip direction="top" offset={[0, -6]}>
              {s.name}
            </Tooltip>
          </CircleMarker>
        ))}
        {pois.map((poi) => (
          <Marker
            key={poi.id}
            position={[poi.latitude, poi.longitude]}
            icon={poiIcon(poi.id === selectedId, readyIds.has(poi.id))}
            title={t(poi.title)}
            alt={t(poi.title)}
            zIndexOffset={poi.id === selectedId ? 500 : 0}
            eventHandlers={{ click: () => onSelect(poi.id) }}
          >
            <Tooltip direction="top" offset={[0, -16]}>
              {t(poi.title)}
            </Tooltip>
          </Marker>
        ))}
      </MapContainer>
    </div>
  )
}
