import type { ReactNode } from 'react'
import { Button, Modal, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../../../hooks'

interface ConfirmModalProps {
  title: string
  /** Đã dịch sẵn; nói rõ thao tác sẽ thay đổi gì. */
  message: string
  confirmLabel: string
  /** `danger` cho thao tác tắt/xoá; `primary` cho thao tác thường (vd đóng cảnh báo). */
  tone?: 'danger' | 'primary'
  action: () => Promise<unknown>
  onDone: () => void
  onClose: () => void
  /** Nội dung thêm dưới thông điệp, vd lưu ý còn chuyến chưa quyết định. */
  children?: ReactNode
}

/** Hỏi lại trước một thao tác; nếu server từ chối thì giữ hộp thoại mở kèm lý do. */
export default function ConfirmModal({
  title,
  message,
  confirmLabel,
  tone = 'danger',
  action,
  onDone,
  onClose,
  children,
}: ConfirmModalProps) {
  const { run, busy, error } = useSubmit()

  const confirm = async () => {
    const done = await run(async () => {
      await action()
      return true
    })
    if (done) onDone()
  }

  return (
    <Modal title={title} onClose={onClose}>
      <div className="space-y-3">
        <p className="text-sm text-on-surface-variant">{message}</p>
        {children}
        {error && <Notice tone="error">{error}</Notice>}
      </div>
      <div className="mt-4 flex justify-end gap-2">
        <Button size="sm" variant="neutral" onClick={onClose}>
          {t('Cancel')}
        </Button>
        <Button size="sm" variant={tone} disabled={busy} onClick={confirm}>
          {busy ? t('Working…') : t(confirmLabel)}
        </Button>
      </div>
    </Modal>
  )
}
