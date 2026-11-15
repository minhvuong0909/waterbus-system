import { useId, useState } from 'react'
import type { LatLng } from '../../../constant/riverPaths'
import type { BoatFix, TrackedStop } from '../../tracking/types'
import { illustrationPoint, routeProgress } from '../routeGeometry'
import { formatGpsTime } from '../utils'

const RIVER =
  'M -100 380 Q 15 293 100 292 C 290 295 300 183 485 175 C 590 168 640 250 730 155 C 790 90 755 48 880 48 Q 965 60 1100 -40'
const LINE = 'M 100 292 C 290 295 300 183 485 175 C 590 168 640 250 730 155 C 790 90 755 48 880 48'

/** Sơ đồ tuyến theo mẫu thiết kế; không dùng làm hải đồ điều hướng. */
export default function CaptainRouteIllustration({
  stops,
  path,
  position,
  fresh,
  follow,
}: {
  stops: TrackedStop[]
  path: LatLng[]
  position: BoatFix | null
  fresh: boolean
  follow: boolean
}) {
  const id = useId().replace(/:/g, '')
  const [zoom, setZoom] = useState(1)
  const vessel = position
    ? illustrationPoint(routeProgress([position.latitude, position.longitude], path))
    : null
  const center = follow && vessel ? vessel : [500, 175]
  return (
    <div className="captain-illustration">
      <svg
        viewBox="0 0 1000 350"
        preserveAspectRatio="none"
        role="img"
        aria-label="Sơ đồ hành trình với vị trí tàu chiếu theo đường sông"
      >
        <defs>
          <pattern
            id={`${id}-blocks`}
            width="106"
            height="91"
            patternUnits="userSpaceOnUse"
            patternTransform="rotate(-17)"
          >
            <rect width="106" height="91" fill="#e2e7ed" />
            <path
              d="M0 0H106V91H0ZM33 0V91M70 0V91M0 30H106M0 60H106"
              fill="none"
              stroke="#f8fafc"
              strokeWidth="2"
            />
            <path d="m8 8 17 0v14H8Zm36 30h18v15H44Zm34 30h20v14H78Z" fill="#d9e0e8" />
          </pattern>
          <linearGradient id={`${id}-water`} x2="0" y2="1">
            <stop stopColor="#93d8ec" />
            <stop offset="1" stopColor="#65cde7" />
          </linearGradient>
          <linearGradient id={`${id}-tower`} x2="1" y2="0">
            <stop stopColor="#83bad5" />
            <stop offset="1" stopColor="#b7d5e8" />
          </linearGradient>
        </defs>
        <g transform={`translate(500 175) scale(${zoom}) translate(${-center[0]} ${-center[1]})`}>
          <rect x="-500" y="-400" width="2000" height="1200" fill={`url(#${id}-blocks)`} />
          <g fill="#bfe2a9">
            <path d="m65 48 51-12 18 38-57 13ZM610 24l55-10 17 33-42 17ZM349 271l38-28 51 17-17 37-42-5ZM886 241l29-21 38 32-14 32-43-14ZM188 174l27-14 26 30-25 26Z" />
          </g>
          <g stroke="#fff" fill="none" strokeWidth="7" opacity=".95">
            <path d="M-30 115Q330 45 640 84T1060 110M-20 220Q240 140 470 240T1060 310M100-40 240 410M450-30 560 410M780-40 890 420M-20 340 1020 2" />
          </g>
          <g stroke="#cbd4df" fill="none" strokeWidth="1.5">
            <path d="M-30 115Q330 45 640 84T1060 110M-20 220Q240 140 470 240T1060 310M100-40 240 410M450-30 560 410M780-40 890 420" />
          </g>
          <path d={RIVER} stroke="#bde8f4" strokeWidth="79" fill="none" />
          <path d={RIVER} stroke={`url(#${id}-water)`} strokeWidth="73" fill="none" />
          <g fill={`url(#${id}-tower)`} stroke="#edf5fb" strokeWidth="1">
            <path d="M36 247v-35h13v-12h9v47Zm27 0v-52h17v52Zm25 0 14-112 11 112Zm30 0v-39h13v39Zm19 0v-26h18v26Zm22 0v-45h13v45Z" />
            <path d="m102 135 7-8 2 35h-13Z" />
          </g>
          <g stroke="#e7f2f9" strokeWidth="1">
            <path d="M99 161h11m-12 7h13m-14 7h15m-16 7h17m-18 7h19m-20 7h21m-22 7h23m-24 7h25m-26 7h27m-28 7h29" />
          </g>
          <path
            d={LINE}
            stroke="#029c98"
            strokeWidth="3.5"
            fill="none"
            strokeDasharray="7 8"
            strokeLinecap="round"
          />
          <text x="345" y="80" fill="#66758b" fontSize="13" fontWeight="600">
            SÔNG SÀI GÒN
          </text>
          {stops.map((s) => {
            const [x, y] = illustrationPoint(routeProgress([s.latitude, s.longitude], path))
            const width = Math.max(68, s.stationName.length * 7 + 18)
            return (
              <g key={s.callId}>
                <title>{s.stationName}</title>
                <circle cx={x} cy={y} r="10" fill="#fff" stroke="#059f9c" strokeWidth="3" />
                <circle cx={x} cy={y} r="5.5" fill="#0cafa7" />
                <rect
                  x={x - width / 2}
                  y={y - 42}
                  width={width}
                  height="26"
                  rx="6"
                  fill="#fff"
                  stroke="#009f98"
                />
                <text
                  x={x}
                  y={y - 24}
                  textAnchor="middle"
                  fill="#10233f"
                  fontSize="13"
                  fontWeight="800"
                >
                  {s.stationName}
                </text>
              </g>
            )
          })}
          {vessel && (
            <g transform={`translate(${vessel[0]} ${vessel[1]})`}>
              <title>
                Vị trí GPS: {position?.latitude}, {position?.longitude}
              </title>
              {!fresh && <circle r="30" fill="#163a5740" />}
              <g transform="translate(-31 -23) rotate(-12 32 22)" opacity={fresh ? 1 : 0.7}>
                <path
                  d="m2 32 12-17 40-3 9 11-8 13-40 6Z"
                  fill="#fff"
                  stroke="#16334b"
                  strokeWidth="1.5"
                />
                <path d="m3 32 57-9-6 11-39 6Z" fill="#ffcc16" />
                <path d="m17 16 6-10 19-2 8 9Z" fill="#fff" stroke="#16334b" strokeWidth="1.5" />
                <path d="m21 16 4-7 8-1-1 7Zm16-8 5-1 5 6-11 2Z" fill="#12334d" />
                <path d="m13 37 40-6" stroke="#16334b" strokeWidth="2" />
              </g>
              {!fresh && position && (
                <g>
                  <rect
                    x="-78"
                    y="-55"
                    width="156"
                    height="25"
                    rx="5"
                    fill="#fff"
                    stroke="#8799ad"
                  />
                  <text x="0" y="-38" textAnchor="middle" fontSize="12" fill="#10233f">
                    Vị trí cuối · {formatGpsTime(position.observedAt)}
                  </text>
                </g>
              )}
            </g>
          )}
        </g>
      </svg>
      <span className="captain-illustration-caption">Sơ đồ tuyến · Vị trí chiếu theo GPS</span>
      <div className="captain-map-zoom">
        <button
          type="button"
          aria-label="Phóng to sơ đồ"
          disabled={zoom >= 2}
          onClick={() => setZoom((z) => Math.min(2, z + 0.25))}
        >
          +
        </button>
        <button
          type="button"
          aria-label="Thu nhỏ sơ đồ"
          disabled={zoom <= 1}
          onClick={() => setZoom((z) => Math.max(1, z - 0.25))}
        >
          −
        </button>
      </div>
    </div>
  )
}
