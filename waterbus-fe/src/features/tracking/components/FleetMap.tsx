import { useEffect, useState } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../../constant/env'
import { CircleMarker, MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap, useMapEvents } from 'react-leaflet'
import { t } from '../../../i18n'

export interface MapStation {
  id: string
  name: string
  latitude: number
  longitude: number
}

/** Địa danh giới thiệu trên chuyến tham quan: mốc nhỏ, tàu tới gần thì phát audio. */
export interface MapLandmark {
  name: string
  latitude: number
  longitude: number
}

export interface MapBoat {
  tripId: string
  code: string
  latitude: number
  longitude: number
  /** Mất tín hiệu: vẽ mờ để không lẫn với tàu đang báo vị trí. */
  stale?: boolean
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
  /** Địa danh của chuyến tham quan: ghim cam khi phóng to, chấm nhỏ khi thu nhỏ. */
  landmarks?: MapLandmark[]
  /** Tàu đang được chọn ở danh sách bên cạnh: bản đồ bay tới tàu, các tàu khác mờ đi. */
  selectedBoatId?: string | null
  className?: string
}

/** Boat icon: a teal disc with a boat glyph, so no image files are needed. */
function boatIcon(variant: 'live' | 'stale' | 'selected') {
  const modifier = variant === 'live' ? '' : ` fleet-boat__disc--${variant}`
  return L.divIcon({
    className: 'fleet-boat',
    html: `<span class="fleet-boat__disc${modifier}"><span class="material-symbols-outlined">directions_boat</span></span>`,
    iconSize: [34, 34],
    iconAnchor: [17, 17],
  })
}
const BOAT_ICONS = { live: boatIcon('live'), stale: boatIcon('stale'), selected: boatIcon('selected') }

/** Mốc địa danh: ghim giọt nước màu cam vẽ bằng CSS (không cần icon font), mũi nhọn chỉ đúng vị trí. */
const landmarkPin = L.divIcon({
  className: 'fleet-landmark',
  html: '<span class="fleet-landmark__pin"></span>',
  iconSize: [28, 34],
  iconAnchor: [14, 31],
})
/** Thu nhỏ bản đồ: chỉ một chấm cam để không rối. */
const landmarkDot = L.divIcon({
  className: 'fleet-landmark',
  html: '<span class="fleet-landmark__dot"></span>',
  iconSize: [12, 12],
  iconAnchor: [6, 6],
})

/** Từ mức zoom này ghim địa danh mới hiện đầy đủ. */
const LANDMARK_PIN_ZOOM = 14

// Used until there are stations to fit the view to: the middle of the Saigon River corridor.
const DEFAULT_CENTER: [number, number] = [10.81, 106.73]

/** Ghim địa danh khi phóng to, chấm nhỏ khi thu nhỏ. */
function LandmarkLayer({ landmarks }: { landmarks: MapLandmark[] }) {
  const map = useMap()
  const [zoom, setZoom] = useState(map.getZoom())
  useMapEvents({ zoomend: () => setZoom(map.getZoom()) })
  const pinned = zoom >= LANDMARK_PIN_ZOOM
  return (
    <>
      {landmarks.map((l) => (
        <Marker key={l.name} position={[l.latitude, l.longitude]} icon={pinned ? landmarkPin : landmarkDot}>
          <Tooltip direction="top" offset={[0, pinned ? -28 : -6]}>
            {l.name}
          </Tooltip>
        </Marker>
      ))}
    </>
  )
}

/**
 * Tàu có transition 1 giây để di chuyển mượt giữa hai lần GPS. Khi bản đồ đang zoom (kể cả flyTo), Leaflet đặt lại vị trí tàu
 * từng khung hình, transition làm tàu trượt khỏi tuyến; nên tắt transition trong lúc zoom (class CSS trên khung bản đồ).
 */
function ZoomingClass() {
  const map = useMap()
  useMapEvents({
    zoomstart: () => map.getContainer().classList.add('fleet-map--zooming'),
    zoomend: () => map.getContainer().classList.remove('fleet-map--zooming'),
  })
  return null
}

/** Bay tới tàu được chọn ở danh sách. */
function FocusBoat({ boat }: { boat: MapBoat | undefined }) {
  const map = useMap()
  const latitude = boat?.latitude
  const longitude = boat?.longitude
  // Chỉ bay khi chọn tàu khác; tàu chạy thì bản đồ không giật theo từng vị trí.
  const code = boat?.code
  useEffect(() => {
    if (latitude != null && longitude != null) {
      map.flyTo([latitude, longitude], Math.max(map.getZoom(), 14), { duration: 0.8 })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [code, map])
  return null
}

function LayerToggle({ label, color, checked, onChange }: { label: string; color: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="group flex w-full items-center gap-2.5 rounded-lg px-1.5 py-1 text-left transition-colors hover:bg-black/5 focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-teal-flow"
    >
      <span
        className={`h-3 w-3 shrink-0 rounded-full border-2 border-white shadow ring-1 ring-black/10 transition-opacity ${checked ? '' : 'opacity-35'}`}
        style={{ background: color }}
      />
      <span className={`flex-1 text-xs font-semibold transition-colors ${checked ? 'text-deep-river' : 'text-on-surface-variant'}`}>{label}</span>
      <span className={`relative h-4 w-7 shrink-0 rounded-full transition-colors ${checked ? 'bg-teal-flow' : 'bg-outline-variant'}`}>
        <span
          className={`absolute top-0.5 h-3 w-3 rounded-full bg-white shadow transition-all ${checked ? 'left-[14px]' : 'left-0.5'}`}
        />
      </span>
    </button>
  )
}

/** Bản đồ hành lang sông: tuyến, bến, địa danh và tàu. Có hộp chú thích để bật/tắt từng lớp cho đỡ rối. */
export default function FleetMap({ stations, boats, path, landmarks = [], selectedBoatId = null, className = '' }: FleetMapProps) {
  const [showStations, setShowStations] = useState(true)
  const [showLandmarks, setShowLandmarks] = useState(true)
  const [showBoats, setShowBoats] = useState(true)
  const stationPoints = stations.map((s): [number, number] => [s.latitude, s.longitude])
  const line = path && path.length > 1 ? path : stationPoints
  const bounds = line.length > 1 ? L.latLngBounds(line).pad(0.2) : undefined
  const selected = boats.find((b) => b.tripId === selectedBoatId)

  return (
    <div className="relative h-full min-h-[24rem] w-full">
      <MapContainer
        center={DEFAULT_CENTER}
        zoom={12}
        bounds={bounds}
        scrollWheelZoom
        className={`z-0 h-full min-h-[24rem] w-full rounded-xl ${className}`.trim()}
      >
        <TileLayer attribution={MAP_TILE_ATTRIBUTION} url={MAP_TILE_URL} maxNativeZoom={16} maxZoom={18} />
        {/* Viền trắng dưới đường tuyến để tách khỏi nền bản đồ. */}
        <Polyline positions={line} pathOptions={{ color: '#fff', weight: 7, opacity: 0.9 }} />
        <Polyline positions={line} pathOptions={{ color: '#147A7E', weight: 3.5, opacity: 1 }} />
        {showStations &&
          stations.map((s) => (
            <CircleMarker
              key={s.id}
              center={[s.latitude, s.longitude]}
              radius={5}
              pathOptions={{ color: '#0D2538', weight: 2.5, fillColor: '#fff', fillOpacity: 1 }}
            >
              <Tooltip direction="top" offset={[0, -6]}>
                {s.name}
              </Tooltip>
            </CircleMarker>
          ))}
        {showLandmarks && landmarks.length > 0 && <LandmarkLayer landmarks={landmarks} />}
        {showBoats &&
          boats.map((b) => {
            const isSelected = b.tripId === selectedBoatId
            return (
              <Marker
                key={b.tripId}
                position={[b.latitude, b.longitude]}
                icon={isSelected ? BOAT_ICONS.selected : b.stale ? BOAT_ICONS.stale : BOAT_ICONS.live}
                opacity={selectedBoatId && !isSelected ? 0.45 : 1}
                zIndexOffset={isSelected ? 1000 : 0}
              >
                <Tooltip direction="top" offset={[0, -14]}>
                  {b.code}
                </Tooltip>
              </Marker>
            )
          })}
        <ZoomingClass />
        <FocusBoat boat={selected} />
      </MapContainer>

      <div className="absolute right-3 top-3 z-[1000] w-36 space-y-0.5 rounded-xl bg-white/95 p-1.5 shadow-lg ring-1 ring-black/5 backdrop-blur-sm">
        <LayerToggle label={t('Piers')} color="#fff" checked={showStations} onChange={setShowStations} />
        {landmarks.length > 0 && (
          <LayerToggle label={t('Landmarks')} color="#f59e0b" checked={showLandmarks} onChange={setShowLandmarks} />
        )}
        <LayerToggle label={t('Boats')} color="#147a7e" checked={showBoats} onChange={setShowBoats} />
      </div>
    </div>
  )
}
