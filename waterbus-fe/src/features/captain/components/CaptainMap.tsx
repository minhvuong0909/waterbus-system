import { useState } from 'react'
import { SIGHTSEEING_POIS } from '../../../constant/sightseeingPois'
import { riverPathOf } from '../../../constant/riverPaths'
import FleetMap from '../../tracking/components/FleetMap'
import type { BoatFix, TrackedStop } from '../../tracking/types'
import { formatGpsTime } from '../utils'
import Icon from './CaptainIcon'

const OWN_BOAT_ID = 'own-boat'

/**
 * Bản đồ hành trình của thuyền trưởng. Dùng chung bản đồ với Admin (nền, icon tàu, bến, ghim địa danh) nhưng chỉ vẽ:
 * tàu của mình, các bến của chuyến và, với chuyến tham quan, các địa danh được giới thiệu.
 */
export default function CaptainMap({
  stops,
  routeCode,
  tripType,
  boatCode,
  position,
  fresh,
}: {
  stops: TrackedStop[]
  routeCode: string
  tripType: 'Commuter' | 'Sightseeing'
  boatCode: string
  position: BoatFix | null
  fresh: boolean
}) {
  const [follow, setFollow] = useState(false)
  return (
    <section className="captain-card captain-map-card" aria-label="Bản đồ hành trình">
      <div className="captain-card-heading">
        <h2>
          <Icon name="map" />
          Bản đồ hành trình
        </h2>
        <div className="captain-map-tools">
          <span className={`captain-pill ${fresh ? 'is-success' : 'is-warning'}`}>
            {fresh ? 'GPS trực tiếp' : position ? 'Vị trí cuối cùng' : 'Chờ vị trí GPS'}
          </span>
          {position && (
            <span className="captain-muted">Cập nhật {formatGpsTime(position.observedAt)}</span>
          )}
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
      <div className="captain-map-surface">
        <FleetMap
          stations={stops.map((s) => ({ id: s.callId, name: s.stationName, latitude: s.latitude, longitude: s.longitude }))}
          boats={
            position
              ? [{ tripId: OWN_BOAT_ID, code: boatCode, latitude: position.latitude, longitude: position.longitude, stale: !fresh }]
              : []
          }
          path={riverPathOf(routeCode)}
          landmarks={tripType === 'Sightseeing' ? SIGHTSEEING_POIS : []}
          selectedBoatId={follow ? OWN_BOAT_ID : null}
          showLayerControl={false}
          className="captain-leaflet"
        />
        {!position && (
          <div className="captain-map-note">
            <Icon name="location_searching" />
            Chưa nhận vị trí tàu. Bật chia sẻ GPS trên thiết bị ở tàu.
          </div>
        )}
      </div>
    </section>
  )
}
