import { useState } from 'react'
import { SelectField } from '../../../components/ui'
import { t } from '../../../i18n'
import type { AdminBoat } from '../../operations/types'
import { assignBoatCaptain } from '../services/personnelService'
import type { Captain } from '../types/personnel'
import ReasonConfirmModal from './ReasonConfirmModal'

const REMOVE = ''

/**
 * Gán Captain cho một tàu, hoặc gỡ khỏi tàu hiện tại (BR-CAP-03/04): Captain phải Active, giấy phép còn hạn, mỗi Captain
 * tối đa một tàu, và không đổi/gỡ khi tàu có chuyến đang đón khách, chạy hoặc tạm dừng. Server là nơi quyết định; lỗi hiện ngay trong hộp thoại.
 */
export default function AssignCaptainDialog({
  captain,
  boats,
  captainNames,
  onDone,
  onClose,
}: {
  captain: Captain
  boats: AdminBoat[]
  /** Tên Captain theo id, để báo tàu đang có Captain khác. */
  captainNames: Map<string, string>
  onDone: () => void
  onClose: () => void
}) {
  const current = boats.find((boat) => boat.id === captain.boatId)
  const candidates = boats.filter((boat) => boat.isActive)
  const [choice, setChoice] = useState(current?.id ?? candidates[0]?.id ?? REMOVE)
  const removing = choice === REMOVE
  const unchanged = !removing && choice === current?.id

  const labelOf = (boat: AdminBoat) => {
    const holder = boat.captainUserId && boat.captainUserId !== captain.id ? captainNames.get(boat.captainUserId) : undefined
    return holder ? `${boat.code} · ${boat.name} — ${t('currently {name}', { name: holder })}` : `${boat.code} · ${boat.name}`
  }

  const title = removing ? 'Remove captain from boat' : current ? 'Change captain boat' : 'Assign captain to boat'
  return (
    <ReasonConfirmModal
      title={title}
      message={
        removing
          ? t('Remove {name} from boat {boat}? The boat stays without a Captain and cannot be sold until one is assigned.', {
              name: captain.fullName, boat: current?.code ?? '',
            })
          : t('Assign {name} to the selected boat. A Captain can drive only one boat, so remove them from their current boat first to move them.', {
              name: captain.fullName,
            })
      }
      confirmLabel={removing ? 'Remove from boat' : 'Assign to boat'}
      tone={removing ? 'danger' : 'primary'}
      action={(reason) => {
        if (removing && current) return assignBoatCaptain(current.id, null, reason)
        return assignBoatCaptain(choice, captain.id, reason)
      }}
      onDone={onDone}
      onClose={onClose}
    >
      <SelectField label="Boat" value={choice} onChange={(e) => setChoice(e.target.value)}
        error={unchanged ? t('The Captain is already on this boat.') : undefined}>
        {current && <option value={REMOVE}>{t('— Remove from {boat}', { boat: current.code })}</option>}
        {candidates.map((boat) => (
          <option key={boat.id} value={boat.id}>{labelOf(boat)}</option>
        ))}
      </SelectField>
    </ReasonConfirmModal>
  )
}
