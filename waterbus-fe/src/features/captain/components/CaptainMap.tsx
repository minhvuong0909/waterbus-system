import { useEffect, useMemo, useState } from 'react'
import L from 'leaflet'
import {
  CircleMarker,
  MapContainer,
  Marker,
  Polyline,
  TileLayer,
  Tooltip,
  useMap,
  ZoomControl,
} from 'react-leaflet'
import 'leaflet/dist/leaflet.css'
import Icon from './CaptainIcon'
import { MAP_TILE_ATTRIBUTION, MAP_TILE_URL } from '../../../constant/env'
import { riverPathOf } from '../../../constant/riverPaths'
import type { BoatFix, TrackedStop } from '../../tracking/types'
import { formatGpsTime } from '../utils'
import CaptainRouteIllustration from './CaptainRouteIllustration'

const vesselIcon = L.divIcon({
  className: 'captain-vessel',
  iconSize: [64, 42],
  iconAnchor: [32, 21],
  html: '<svg viewBox="0 0 80 48" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M5 35 17 19 62 14 72 26 66 37 20 43Z" fill="white" stroke="#102a43" stroke-width="2"/><path d="m7 34 62-9-6 11-43 6Z" fill="#ffcf25"/><path d="m21 20 5-10 24-3 9 10Z" fill="#fff" stroke="#102a43" stroke-width="2"/><path d="m25 21 4-8 9-1-1 8Zm17-10 8-1 5 8-14 2Z" fill="#12334d"/><path d="m17 39 47-7" stroke="#102a43" stroke-width="2"/><path d="m34 6 12-2" stroke="white" stroke-width="3" stroke-linecap="round"/></svg>',
})

function Viewport({
  stops,
  position,
  follow,
}: {
  stops: TrackedStop[]
  position: BoatFix | null
  follow: boolean
}) {
  const map = useMap()
  const key = stops.map((s) => `${s.callId}:${s.latitude}:${s.longitude}`).join('|')
  useEffect(() => {
    const points = stops.map((s): [number, number] => [s.latitude, s.longitude])
    if (points.length > 0) map.fitBounds(L.latLngBounds(points), { padding: [55, 45], maxZoom: 15 })
    const observer = new ResizeObserver(() => map.invalidateSize())
    observer.observe(map.getContainer())
    return () => observer.disconnect()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, key])
  useEffect(() => {
    if (follow && position) map.panTo([position.latitude, position.longitude], { animate: false })
  }, [map, follow, position])
  return null
}

export default function CaptainMap({
  stops,
  routeCode,
  position,
  fresh,
}: {
  stops: TrackedStop[]
  routeCode: string
  position: BoatFix | null
  fresh: boolean
}) {
  const [follow, setFollow] = useState(false)
  const [tileError, setTileError] = useState(false)
  const [geographic, setGeographic] = useState(false)
  // Chỉ vẽ đường sông đã biết và cắt theo đoạn phục vụ, không nối thẳng xuyên đất.
  const path = useMemo(() => {
    const river = riverPathOf(routeCode)
    if (!river || stops.length < 2) return undefined
    const nearest = (s: TrackedStop) =>
      river.reduce(
        (best, p, i) =>
          Math.hypot(p[0] - s.latitude, p[1] - s.longitude) <
          Math.hypot(river[best][0] - s.latitude, river[best][1] - s.longitude)
            ? i
            : best,
        0,
      )
    const first = nearest(stops[0])
    const last = nearest(stops[stops.length - 1])
    if (first === last) return undefined
    const slice = river.slice(Math.min(first, last), Math.max(first, last) + 1)
    return first > last ? slice.reverse() : slice
  }, [stops, routeCode])
  return (
    <section className="captain-card captain-map-card" aria-label="Bản đồ hành trình">
      <div className="captain-card-heading">
        <h2>
          <Icon name="map" />
          Bản đồ hành trình
        </h2>
        <div className="captain-map-tools">
          {path && (
            <button
              type="button"
              className="captain-outline"
              onClick={() => setGeographic((v) => !v)}
            >
              {geographic ? 'Sơ đồ tuyến' : 'Bản đồ thực'}
            </button>
          )}
          <span className={`captain-pill ${fresh ? 'is-success' : 'is-warning'}`}>
            {fresh ? 'GPS trực tiếp' : position ? 'Vị trí cuối cùng' : 'Chờ vị trí GPS'}
          </span>
          <button
            className="captain-outline"
            type="button"
            disabled={!position}
            aria-pressed={follow}
            onClick={() => setFollow((v) => !v)}
          >
            <Icon name="my_location" />
            {follow ? 'Đang theo dõi' : 'Theo dõi tàu'}
          </button>
        </div>
      </div>
      <div className={`captain-map-surface ${path && !geographic ? 'is-illustrated' : ''}`}>
        {path && !geographic ? (
          <CaptainRouteIllustration
            stops={stops}
            path={path}
            position={position}
            fresh={fresh}
            follow={follow}
          />
        ) : (
          <>
            <MapContainer
              center={[10.8, 106.72]}
              zoom={13}
              zoomControl={false}
              scrollWheelZoom={false}
              className="captain-leaflet"
            >
              <TileLayer
                attribution={MAP_TILE_ATTRIBUTION}
                url={MAP_TILE_URL}
                eventHandlers={{ tileerror: () => setTileError(true) }}
              />
              <Viewport stops={stops} position={position} follow={follow} />
              {path && (
                <Polyline
                  positions={path}
                  pathOptions={{ color: '#049d98', weight: 4, dashArray: '7 10', lineCap: 'round' }}
                />
              )}
              {stops.map((s) => (
                <CircleMarker
                  key={s.callId}
                  center={[s.latitude, s.longitude]}
                  radius={8}
                  pathOptions={{ color: '#009f98', weight: 3, fillColor: '#fff', fillOpacity: 1 }}
                >
                  <Tooltip
                    permanent
                    direction="top"
                    offset={[0, -10]}
                    className="captain-station-label"
                  >
                    {s.stationName}
                  </Tooltip>
                </CircleMarker>
              ))}
              {position && (
                <Marker
                  position={[position.latitude, position.longitude]}
                  icon={vesselIcon}
                  opacity={fresh ? 1 : 0.65}
                >
                  <Tooltip permanent={!fresh} direction="top" offset={[0, -20]}>
                    Vị trí {fresh ? 'lúc' : 'cuối'} · {formatGpsTime(position.observedAt)}
                  </Tooltip>
                </Marker>
              )}
              <ZoomControl position="bottomright" />
            </MapContainer>
          </>
        )}
        {!position && (
          <div className="captain-map-note">
            <Icon name="location_searching" />
            Chưa nhận vị trí tàu. Bật chia sẻ GPS trên thiết bị ở tàu.
          </div>
        )}
        {tileError && geographic && (
          <div className="captain-map-note">
            Không tải được một số ô bản đồ. Kiểm tra kết nối Internet.
          </div>
        )}
      </div>
      {!path && (
        <p className="captain-map-caption">
          Hiển thị vị trí các bến; tuyến này chưa có đường hành trình trên sông.
        </p>
      )}
    </section>
  )
}
