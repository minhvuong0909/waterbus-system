import type { TrackedStop } from '../../tracking/types'

/** Danh sách bến của chuyến: bến đã rời có dấu tích, bến tiếp theo được tô đậm. */
export default function CaptainStopList({
  stops,
  nextId,
  clock,
}: {
  stops: TrackedStop[]
  nextId?: string
  clock: (iso?: string | null) => string
}) {
  return (
    <section className="cap-panel" aria-label="Các bến của chuyến">
      <div className="cap-panel-head">
        <h2>Các bến</h2>
      </div>
      <ol className="cap-stops">
        {stops.map((stop) => {
          const departed = stop.status === 'Departed'
          return (
            <li key={stop.callId} className={`${departed ? 'is-departed' : ''} ${stop.callId === nextId ? 'is-next' : ''}`}>
              <span className="cap-stop-dot" aria-hidden="true" />
              <div>
                <strong>{stop.stationName}</strong>
                <small>{departed ? 'Đã rời bến' : stop.callId === nextId ? 'Bến tiếp theo' : 'Chưa đến'}</small>
              </div>
              <time>{clock(stop.estimatedArrivalTime ?? stop.estimatedDepartureTime)}</time>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
