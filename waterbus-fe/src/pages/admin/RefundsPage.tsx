import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, CopyButton, Notice, PageHeader, Panel, TextField } from '../../components/ui'
import { useSubmit } from '../../features/admin/hooks/useAdmin'
import {
  confirmManualRefund,
  reconcileRefund,
  requestRefund,
} from '../../features/admin/services/refundService'
import type { ManualRefundResult, RefundRequestResult } from '../../features/admin/services/refundService'
import { isGuid } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { formatVnd } from '../../utils/format'
import { formatDateTime } from '../../utils/time'

/**
 * Admin hoàn tiền Booking của chuyến bị huỷ/chấm dứt (BR-ROLE-04, BR-RF-07). Khi cổng thanh toán
 * không hoàn được, Admin hoàn tay rồi xác nhận kèm bằng chứng (BR-RF-10).
 */
export default function RefundsPage() {
  useDocumentTitle('Refunds')
  const [bookingId, setBookingId] = useState('')
  const [reason, setReason] = useState('')
  const [result, setResult] = useState<RefundRequestResult | null>(null)
  const requesting = useSubmit()

  const validId = isGuid(bookingId.trim())
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!validId || !reason.trim()) return
    const created = await requesting.run(() => requestRefund(bookingId.trim(), reason.trim()))
    if (created) setResult(created)
  }

  return (
    <>
      <PageHeader title="Refunds" description="Refund a booking of a cancelled or terminated trip." />

      <Panel title="New refund">
        <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Booking ID"
            hint="The booking's ID (GUID), from the order or the customer's request."
            value={bookingId}
            onChange={(e) => setBookingId(e.target.value)}
            error={bookingId.trim() && !validId ? t('Enter a valid booking ID.') : undefined}
            spellCheck={false}
            required
          />
          <TextField
            label="Reason"
            placeholder={t('e.g. Trip cancelled because of bad weather')}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            maxLength={500}
            required
          />
          <div className="flex flex-wrap items-center gap-3 sm:col-span-2">
            <Button type="submit" icon="receipt_long" disabled={requesting.busy || !validId || !reason.trim()}>
              {requesting.busy ? t('Sending…') : t('Request refund')}
            </Button>
            {requesting.error && <Notice tone="error">{requesting.error}</Notice>}
          </div>
        </form>
      </Panel>

      {result && <RefundResult result={result} />}
      {/* key: điền sẵn mã hoàn tiền vừa tạo khi có kết quả mới. */}
      <ReconcilePanel key={result?.refundTransactionId ?? 'none'} defaultId={result?.refundTransactionId ?? ''} />
    </>
  )
}

function RefundResult({ result }: { result: RefundRequestResult }) {
  return (
    <Panel title="Refund created" className="mt-4">
      <dl className="grid gap-3 text-sm sm:grid-cols-4">
        <Fact label="Amount" value={formatVnd(result.amount)} />
        <Fact label="Status" value={t(result.status)} />
        <Fact label="Gateway" value={result.gateway} />
        <div>
          <dt className="text-2xs font-semibold uppercase tracking-wide text-on-surface-variant">{t('Refund ID')}</dt>
          <dd className="mt-1 flex items-center gap-1 font-mono text-xs">
            <span className="truncate">{result.refundTransactionId}</span>
            <CopyButton value={result.refundTransactionId} />
          </dd>
        </div>
      </dl>
      {result.tickets.length > 0 && (
        <ul className="mt-4 divide-y divide-outline-variant/30 rounded-xl border border-outline-variant/30">
          {result.tickets.map((ticket) => (
            <li key={ticket.ticketId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
              <span className="font-mono font-semibold">{ticket.ticketCode}</span>
              <span className="text-on-surface-variant">{ticket.reason}</span>
              <span className="font-semibold">{formatVnd(ticket.amount)}</span>
            </li>
          ))}
        </ul>
      )}
      {result.requiresManualAction && <ManualConfirmForm result={result} />}
    </Panel>
  )
}

function ManualConfirmForm({ result }: { result: RefundRequestResult }) {
  const [evidence, setEvidence] = useState('')
  const [reference, setReference] = useState('')
  const [amount, setAmount] = useState(String(result.amount))
  const [done, setDone] = useState<ManualRefundResult | null>(null)
  const confirming = useSubmit()

  const confirmedAmount = Number(amount)
  const valid = evidence.trim().length > 0 && Number.isFinite(confirmedAmount) && confirmedAmount > 0
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!valid) return
    const confirmed = await confirming.run(() =>
      confirmManualRefund({
        refundTransactionId: result.refundTransactionId,
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
          {t('Manual refund confirmed')}: {formatVnd(done.amount)} • {t(done.status)}
          {done.completedAt ? ` • ${formatDateTime(done.completedAt)}` : ''}
        </Notice>
      </div>
    )
  }
  return (
    <form onSubmit={submit} className="mt-5 rounded-xl border border-signal-amber/50 bg-signal-amber/10 p-4">
      <p className="mb-3 text-sm font-semibold text-deep-river">
        {t('The gateway could not refund this payment. Refund it by hand, then confirm with proof.')}
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
        <Button type="submit" variant="dark" disabled={confirming.busy || !valid}>
          {confirming.busy ? t('Sending…') : t('Confirm manual refund')}
        </Button>
        {confirming.error && <Notice tone="error">{confirming.error}</Notice>}
      </div>
    </form>
  )
}

/** Hỏi lại cổng thanh toán trạng thái của một lệnh hoàn đang chờ. */
function ReconcilePanel({ defaultId }: { defaultId: string }) {
  const [refundId, setRefundId] = useState(defaultId)
  const [status, setStatus] = useState<string | null>(null)
  const checking = useSubmit()
  const validId = isGuid(refundId.trim())

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!validId) return
    setStatus(null)
    const latest = await checking.run(() => reconcileRefund(refundId.trim()))
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
            error={refundId.trim() && !validId ? t('Enter a valid refund ID.') : undefined}
            spellCheck={false}
          />
        </div>
        <Button type="submit" variant="neutral" icon="refresh" disabled={checking.busy || !validId}>
          {t('Check with gateway')}
        </Button>
      </form>
      <div className="mt-3">
        {status && <Notice tone="success">{t('Refund status')}: {t(status)}</Notice>}
        {checking.error && <Notice tone="error">{checking.error}</Notice>}
      </div>
    </Panel>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-2xs font-semibold uppercase tracking-wide text-on-surface-variant">{t(label)}</dt>
      <dd className="mt-1 font-semibold text-deep-river">{value}</dd>
    </div>
  )
}
