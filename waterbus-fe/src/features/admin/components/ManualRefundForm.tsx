import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import { formatDateTime } from '../../../utils/time'
import { useSubmit } from '../hooks/useAdmin'
import { confirmManualRefund } from '../services/refundService'
import type { ManualRefundResult, RefundRequestResult } from '../types'

/**
 * Cổng thanh toán không hoàn được: Admin hoàn tay rồi xác nhận kèm bằng chứng và số tiền thực hoàn
 * (BR-RF-10). Backend chặn xác nhận lần hai.
 */
export default function ManualRefundForm({ refund }: { refund: RefundRequestResult }) {
  const [evidence, setEvidence] = useState('')
  const [reference, setReference] = useState('')
  const [amount, setAmount] = useState(String(refund.amount))
  const [done, setDone] = useState<ManualRefundResult | null>(null)
  const { run, busy, error } = useSubmit()

  const confirmedAmount = Number(amount)
  const valid = evidence.trim().length > 0 && Number.isFinite(confirmedAmount) && confirmedAmount > 0
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!valid) return
    const confirmed = await run(() =>
      confirmManualRefund({
        refundTransactionId: refund.refundTransactionId,
        evidence: evidence.trim(),
        gatewayReference: reference.trim() || null,
        confirmedAmount,
      }),
    )
    if (confirmed) setDone(confirmed)
  }

  if (done) {
    return (
      <div className="mt-4">
        <Notice tone="success">
          {done.isDemo ? t('Demo refund confirmed (no real money moved)') : t('Manual refund confirmed')}: {formatVnd(done.amount)} • {t(done.status)}
          {done.completedAt ? ` • ${formatDateTime(done.completedAt)}` : ''}
        </Notice>
      </div>
    )
  }
  return (
    <form onSubmit={submit} className="mt-5 rounded-xl border border-signal-amber/50 bg-signal-amber/10 p-4">
      {refund.isDemo && (
        <Notice tone="error">
          {refund.notice ?? t('DEMO: no real money is transferred.')}
        </Notice>
      )}
      <p className="mb-3 text-sm font-semibold text-deep-river">
        {refund.isDemo
          ? t('Demo mode: do NOT transfer real money. Record the simulated refund with a demo reference.')
          : t('The gateway could not refund this payment. Refund it by hand, then confirm with proof.')}
      </p>
      <div className="grid gap-4 sm:grid-cols-3">
        <TextField
          label="Evidence"
          placeholder={t('e.g. bank transfer slip number')}
          value={evidence}
          onChange={(e) => setEvidence(e.target.value)}
          required
        />
        <TextField label="Gateway reference" value={reference} onChange={(e) => setReference(e.target.value)} />
        <TextField
          label="Amount refunded (VND)"
          type="number"
          min={0}
          inputMode="numeric"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          required
        />
      </div>
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <Button type="submit" variant="dark" disabled={busy || !valid}>
          {busy ? t('Sending…') : refund.isDemo ? t('Confirm demo refund') : t('Confirm manual refund')}
        </Button>
        {error && <Notice tone="error">{error}</Notice>}
      </div>
    </form>
  )
}
