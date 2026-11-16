import { useState } from 'react'
import type { ReactNode } from 'react'
import { TextField } from '../../../components/ui'
import { ApiError } from '../../../services/apiClient'
import { t } from '../../../i18n'
import ConfirmModal from './ConfirmModal'

/** Hỏi lại trước thao tác nhân sự và bắt buộc nhập lý do (BR-CAP-07); lý do được ghi vào audit. */
export default function ReasonConfirmModal({
  title,
  message,
  confirmLabel,
  tone = 'primary',
  action,
  onDone,
  onClose,
  children,
}: {
  title: string
  message: string
  confirmLabel: string
  tone?: 'danger' | 'primary'
  action: (reason: string) => Promise<unknown>
  onDone: () => void
  onClose: () => void
  /** Ô chọn thêm đặt phía trên ô lý do, vd trạng thái mới. */
  children?: ReactNode
}) {
  const [reason, setReason] = useState('')
  return (
    <ConfirmModal
      title={title}
      message={message}
      confirmLabel={confirmLabel}
      tone={tone}
      action={() => {
        if (!reason.trim()) throw new ApiError(400, t('Enter a reason.'))
        return action(reason)
      }}
      onDone={onDone}
      onClose={onClose}
    >
      {children}
      <TextField label="Reason" required maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
    </ConfirmModal>
  )
}
