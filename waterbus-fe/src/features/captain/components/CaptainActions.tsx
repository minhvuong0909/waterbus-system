import Icon from './CaptainIcon'
import type { AdminTrip, CaptainActionHint, OperationType } from '../../operations/types'

export interface CaptainAction {
  type: Extract<OperationType, 'OpenBoarding' | 'Start' | 'Arrive' | 'Complete'>
  icon: string
  label: string
  /** Nội dung ghi vào nhật ký vận hành khi thực hiện. */
  reason: string
}

export const ACTIONS: CaptainAction[] = [
  { type: 'OpenBoarding', icon: 'groups', label: 'Mở đón khách', reason: 'Thuyền trưởng mở đón khách.' },
  { type: 'Start', icon: 'play_arrow', label: 'Khởi hành', reason: 'Thuyền trưởng xác nhận khởi hành.' },
  { type: 'Arrive', icon: 'sports_score', label: 'Đến bến cuối', reason: 'Thuyền trưởng xác nhận đến bến cuối.' },
  { type: 'Complete', icon: 'check_circle', label: 'Kết thúc chuyến', reason: 'Thuyền trưởng kết thúc chuyến.' },
]

/** Bước nào đã xong theo trạng thái chuyến. */
const DONE_STATUSES: Record<CaptainAction['type'], AdminTrip['status'][]> = {
  OpenBoarding: ['Boarding', 'EnRoute', 'Arrived', 'Completed'],
  Start: ['EnRoute', 'Arrived', 'Completed'],
  Arrive: ['Arrived', 'Completed'],
  Complete: ['Completed'],
}

/** Vì sao nút chưa bấm được (mã từ `CaptainOperationRules` của backend); null khi không cần giải thích. */
export function blockedReason(hint: CaptainActionHint | undefined, formatClock: (iso: string) => string): string | null {
  if (!hint || hint.allowed) return null
  switch (hint.reason) {
    case 'NotYet':
      return `Đến ${hint.availableAt ? formatClock(hint.availableAt) : '—'} mới mở đón khách`
    case 'OpenBoardingFirst':
      return 'Cần mở đón khách trước'
    case 'NoPosition':
      return 'Đang chờ GPS của tàu'
    case 'NotAtLastStop':
      return 'Tàu chưa tới bến cuối'
    case 'JourneyNotDone':
      return 'Tàu chưa đi hết hành trình'
    default:
      return null
  }
}

/**
 * Bốn bước của một chuyến theo thứ tự: Mở đón khách → Khởi hành → Đến bến cuối → Kết thúc chuyến.
 * Bước đang làm được tô đậm, bước đã xong có dấu tích, bước chưa tới lượt bị mờ kèm lý do. Backend quyết định bước nào bấm được.
 */
export default function CaptainActions({
  trip,
  allowed,
  reasonOf,
  locked,
  onPick,
  onSos,
}: {
  trip: AdminTrip
  /** Backend cho phép thao tác này lúc này (đã gồm luật của thuyền trưởng). */
  allowed: (type: OperationType) => boolean
  reasonOf: (type: OperationType) => string | null
  /** Mất kết nối hoặc đang xử lý: khóa mọi nút. */
  locked: boolean
  onPick: (action: CaptainAction) => void
  onSos: () => void
}) {
  const current = ACTIONS.find((a) => allowed(a.type))?.type
  return (
    <section className="cap-panel" aria-label="Thao tác vận hành">
      <div className="cap-panel-head">
        <h2>Thao tác vận hành</h2>
        <button type="button" className="cap-sos" disabled={locked} onClick={onSos}>
          <Icon name="e911_emergency" />
          Gửi SOS
        </button>
      </div>
      <ol className="cap-steps">
        {ACTIONS.map((item, index) => {
          const done = DONE_STATUSES[item.type].includes(trip.status)
          const enabled = !done && !locked && allowed(item.type)
          const state = done ? 'done' : item.type === current && !locked ? 'current' : 'idle'
          const note = done ? 'Đã xong' : reasonOf(item.type)
          return (
            <li key={item.type} className={`cap-step is-${state}`}>
              <button type="button" aria-label={item.label} disabled={!enabled} onClick={() => onPick(item)}>
                <span className="cap-step-badge">{done ? <Icon name="check" /> : index + 1}</span>
                <Icon name={item.icon} />
                <strong>{item.label}</strong>
              </button>
              <small>{note ?? ' '}</small>
            </li>
          )
        })}
      </ol>
    </section>
  )
}
