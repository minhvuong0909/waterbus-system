import { useState } from 'react'
import { Button, DataTable, ErrorState, Notice, Panel, TextField } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { t } from '../../../i18n'
import { saveBlob } from '../../../utils/download'
import { formatVnd } from '../../../utils/format'
import { formatDateTime } from '../../../utils/time'
import { useFetch, useReload, useSubmit } from '../../../hooks'
import { confirmRefundsBatch, exportPendingRefundsCsv, listPendingRefunds } from '../services/refundService'
import type { BatchConfirmResult, PendingRefund } from '../types'

/**
 * Các khoản hoàn đang chờ nhân viên hoàn. Admin tải CSV giao việc (cho phép tải lại), và sau khi nhân viên đã hoàn qua cổng
 * merchant thì chọn các khoản đó, nhập bằng chứng rồi xác nhận hàng loạt (BR-RF-10): hệ thống gửi email cho người mua.
 * `refreshKey` đổi khi có yêu cầu hoàn mới được tạo ở nơi khác trên trang.
 */
export default function PendingRefundsPanel({ refreshKey }: { refreshKey: number }) {
  const [version, reload] = useReload()
  const pending = useFetch(() => listPendingRefunds(), [version, refreshKey])
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set())
  const [evidence, setEvidence] = useState('')
  const [downloaded, setDownloaded] = useState<string | null>(null)
  const [result, setResult] = useState<BatchConfirmResult | null>(null)
  const exporting = useSubmit()
  const confirming = useSubmit()

  if (pending.error) return <ErrorState onRetry={pending.retry} />

  const rows = pending.data?.items ?? []
  const byId = new Map(rows.map((row) => [row.refundId, row]))
  // Giữ lại các lựa chọn còn trong danh sách (khoản vừa xác nhận đã rời đi).
  const chosen = rows.filter((row) => selected.has(row.refundId))
  const chosenTotal = chosen.reduce((sum, row) => sum + row.amount, 0)
  const allSelected = rows.length > 0 && chosen.length === rows.length

  const toggle = (id: string) =>
    setSelected((current) => {
      const next = new Set(current)
      if (!next.delete(id)) next.add(id)
      return next
    })

  const download = async () => {
    setDownloaded(null)
    const file = await exporting.run(() => exportPendingRefundsCsv())
    if (!file) return
    saveBlob(file.blob, file.fileName ?? 'hoan-tien.csv')
    setDownloaded(t('Downloaded {count} refunds to hand to staff.', { count: file.rowCount ?? rows.length }))
    reload()
  }

  const confirm = async () => {
    const done = await confirming.run(() =>
      confirmRefundsBatch(
        chosen.map((row) => ({
          refundTransactionId: row.refundId,
          evidence: evidence.trim(),
          gatewayReference: null,
          confirmedAmount: row.amount,
        })),
      ),
    )
    if (!done) return
    setResult(done)
    setSelected(new Set(done.items.filter((item) => !item.succeeded).map((item) => item.refundTransactionId)))
    reload()
  }

  const columns: Column<PendingRefund>[] = [
    {
      header: 'Select',
      render: (x) => (
        <input
          type="checkbox"
          aria-label={t('Select refund {code}', { code: x.bookingCode })}
          checked={selected.has(x.refundId)}
          onChange={() => toggle(x.refundId)}
        />
      ),
    },
    { header: 'Booking', render: (x) => <span className="font-mono font-semibold">{x.bookingCode}</span> },
    {
      header: 'Buyer',
      render: (x) => (
        <>
          <div className="font-semibold">{x.purchaserName}</div>
          <div className="text-xs text-on-surface-variant">{x.purchaserEmail}</div>
        </>
      ),
    },
    {
      header: 'Reason',
      render: (x) =>
        x.isLatePayment ? t('Late payment (no ticket)') : x.tripStatus === 'Terminated' ? t('Trip terminated') : t('Trip cancelled'),
    },
    { header: 'VNPay reference', render: (x) => <span className="font-mono text-xs">{x.merchantReference ?? '—'}</span> },
    { header: 'Exported', render: (x) => (x.lastExportedAt ? formatDateTime(x.lastExportedAt) : t('Not yet')) },
    { header: 'Amount', render: (x) => formatVnd(x.amount), className: 'text-right' },
  ]

  return (
    <Panel title="Refunds waiting for staff" className="mt-4">
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <Button icon="download" variant="dark" onClick={download} disabled={exporting.busy || !pending.data || rows.length === 0}>
          {exporting.busy ? t('Preparing…') : t('Download CSV')}
        </Button>
        {pending.data && <span className="text-sm text-on-surface-variant">{t('{count} refunds waiting', { count: pending.data.totalCount })}</span>}
        {exporting.error && <Notice tone="error">{exporting.error}</Notice>}
        {downloaded && <Notice tone="success">{downloaded}</Notice>}
      </div>

      <DataTable columns={columns} rows={rows} rowKey={(x) => x.refundId} empty="No refunds are waiting for staff." />
      {pending.data && pending.data.totalCount > rows.length && (
        <div className="mt-3">
          <Notice tone="error">{t('Only the first {count} refunds are shown. Confirm these first.', { count: rows.length })}</Notice>
        </div>
      )}

      {rows.length > 0 && (
        <div className="mt-5 rounded-xl border border-signal-amber/50 bg-signal-amber/10 p-4">
          <p className="mb-3 text-sm font-semibold text-deep-river">
            {t('Only select refunds staff have already paid out. Confirming emails each buyer that the money arrives in about 2–3 working days.')}
          </p>
          <label className="mb-3 flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={allSelected}
              onChange={() => setSelected(allSelected ? new Set() : new Set(rows.map((row) => row.refundId)))}
            />
            {t('Select all')}
          </label>
          <TextField
            label="Evidence"
            placeholder={t('e.g. VNPay refund batch of 12 Nov')}
            hint={t('Applied to every selected refund.')}
            value={evidence}
            onChange={(e) => setEvidence(e.target.value)}
            maxLength={500}
          />
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Button
              variant="dark"
              icon="task_alt"
              onClick={confirm}
              disabled={confirming.busy || chosen.length === 0 || evidence.trim().length === 0 || chosen.length > 100}
            >
              {confirming.busy ? t('Sending…') : t('Confirm {count} as refunded', { count: chosen.length })}
            </Button>
            {chosen.length > 0 && <span className="text-sm font-semibold text-deep-river">{formatVnd(chosenTotal)}</span>}
            {chosen.length > 100 && <Notice tone="error">{t('Confirm at most 100 refunds at a time.')}</Notice>}
            {confirming.error && <Notice tone="error">{confirming.error}</Notice>}
          </div>
        </div>
      )}

      {result && (
        <div className="mt-4 space-y-2">
          <Notice tone={result.failed === 0 ? 'success' : 'error'}>
            {t('Confirmed {confirmed}, failed {failed}.', { confirmed: result.confirmed, failed: result.failed })}
          </Notice>
          {result.items.some((item) => !item.succeeded) && (
            <ul className="divide-y divide-outline-variant/30 rounded-xl border border-outline-variant/30 text-sm">
              {result.items
                .filter((item) => !item.succeeded)
                .map((item) => (
                  <li key={item.refundTransactionId} className="px-3 py-2">
                    <span className="font-mono font-semibold">{byId.get(item.refundTransactionId)?.bookingCode ?? item.refundTransactionId}</span>
                    <span className="ml-2 text-on-surface-variant">{item.error}</span>
                  </li>
                ))}
            </ul>
          )}
        </div>
      )}
    </Panel>
  )
}
