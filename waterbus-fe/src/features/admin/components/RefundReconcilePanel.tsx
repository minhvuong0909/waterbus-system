import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, Panel, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { isGuid } from '../../../utils/guid'
import { useSubmit } from '../../../hooks'
import { reconcileRefund } from '../services/refundService'

/** Hỏi lại cổng thanh toán trạng thái của một lệnh hoàn đang chờ (BR-RF-07: đối soát). */
export default function RefundReconcilePanel({ defaultId = '' }: { defaultId?: string }) {
  const [refundId, setRefundId] = useState(defaultId)
  const [status, setStatus] = useState<string | null>(null)
  const { run, busy, error } = useSubmit()

  const id = refundId.trim()
  const validId = isGuid(id)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!validId) return
    setStatus(null)
    const latest = await run(() => reconcileRefund(id))
    if (latest) setStatus(latest)
  }

  return (
    <Panel title="Check a pending refund" className="mt-4">
      <form onSubmit={submit} className="flex flex-col gap-3 sm:flex-row sm:items-end">
        <div className="flex-1">
          <TextField
            label="Refund ID"
            value={refundId}
            onChange={(e) => setRefundId(e.target.value)}
            error={id && !validId ? t('Enter a valid refund ID.') : undefined}
            spellCheck={false}
          />
        </div>
        <Button type="submit" variant="neutral" icon="refresh" disabled={busy || !validId}>
          {t('Check with gateway')}
        </Button>
      </form>
      <div className="mt-3">
        {status && <Notice tone="success">{t('Refund status')}: {t(status)}</Notice>}
        {error && <Notice tone="error">{error}</Notice>}
      </div>
    </Panel>
  )
}
