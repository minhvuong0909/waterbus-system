import { useState } from 'react'
import { Button, DataTable, ErrorState, Modal, Notice, Pagination, Panel, TextField } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import { formatDateTime } from '../../../utils/time'
import { useFetch, useReload, useSubmit } from '../../../hooks'
import { listReconciliationRequired, reconcileRefund, requestRefund } from '../services/refundService'
import type { ReconciliationItem, RefundRequestResult } from '../types'

const PAGE_SIZE = 20

const KIND_LABEL: Record<ReconciliationItem['kind'], string> = {
  LatePayment: 'Late payment',
  RefundProcessing: 'Refund in progress',
}

/** Hỏi lý do rồi tạo yêu cầu hoàn cho Booking của một khoản tiền về muộn (BR-PAY-05, BR-RF-07). */
function LatePaymentRefundForm({
  item,
  onCreated,
  onCancel,
}: {
  item: ReconciliationItem
  onCreated: (result: RefundRequestResult) => void
  onCancel: () => void
}) {
  const [reason, setReason] = useState(t('Payment arrived after the order had expired'))
  const { run, busy, error } = useSubmit()
  const canSubmit = reason.trim().length > 0 && item.bookingId !== null

  return (
    <form
      className="space-y-3"
      onSubmit={async (e) => {
        e.preventDefault()
        if (!canSubmit || !item.bookingId) return
        const created = await run(() => requestRefund(item.bookingId as string, reason.trim()))
        if (created) onCreated(created)
      }}
    >
      <p className="text-sm text-on-surface-variant">
        {t('Refund {amount} paid at {time}.', { amount: formatVnd(item.amount), time: formatDateTime(item.occurredAt) })}
      </p>
      <TextField label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} required />
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <Button size="sm" variant="neutral" onClick={onCancel}>
          {t('Cancel')}
        </Button>
        <Button size="sm" type="submit" disabled={busy || !canSubmit}>
          {busy ? t('Sending…') : t('Request refund')}
        </Button>
      </div>
    </form>
  )
}

/**
 * Các khoản cần Admin đối soát, lấy từ backend thay vì bắt Admin gõ mã GUID: tiền về muộn chưa có lệnh hoàn (tạo yêu cầu
 * hoàn) và lệnh hoàn VNPay còn Processing (hỏi lại cổng). `refreshKey` đổi khi trang có yêu cầu hoàn mới.
 */
export default function ReconciliationPanel({
  refreshKey,
  onRefundCreated,
}: {
  refreshKey: number
  onRefundCreated: (result: RefundRequestResult) => void
}) {
  const [version, reload] = useReload()
  const [pageNumber, setPageNumber] = useState(1)
  const items = useFetch(() => listReconciliationRequired(pageNumber, PAGE_SIZE), [version, refreshKey, pageNumber])
  const [refunding, setRefunding] = useState<ReconciliationItem | null>(null)
  const [checked, setChecked] = useState<{ id: string; status: string } | null>(null)
  const checking = useSubmit()
  const [checkingId, setCheckingId] = useState<string | null>(null)

  if (items.error) {
    return (
      <Panel>
        <ErrorState compact onRetry={items.retry} />
      </Panel>
    )
  }

  const data = items.data
  const check = async (item: ReconciliationItem) => {
    setChecked(null)
    setCheckingId(item.transactionId)
    const status = await checking.run(() => reconcileRefund(item.transactionId))
    setCheckingId(null)
    if (status) {
      setChecked({ id: item.transactionId, status })
      reload()
    }
  }

  const columns: Column<ReconciliationItem>[] = [
    { header: 'Time', render: (x) => formatDateTime(x.occurredAt), className: 'whitespace-nowrap' },
    { header: 'Type', render: (x) => t(KIND_LABEL[x.kind] ?? x.kind) },
    { header: 'VNPay reference', render: (x) => <span className="font-mono text-xs">{x.merchantReference ?? '—'}</span> },
    { header: 'Amount', render: (x) => formatVnd(x.amount), className: 'text-right whitespace-nowrap' },
    {
      header: 'Actions',
      className: 'text-right',
      render: (x) => {
        if (x.kind === 'RefundProcessing') {
          return (
            <Button size="sm" variant="neutral" icon="refresh" disabled={checking.busy} onClick={() => check(x)}>
              {checkingId === x.transactionId ? t('Checking…') : t('Check with gateway')}
            </Button>
          )
        }
        if (x.refundId) {
          return (
            <span className="text-xs text-on-surface-variant">
              {t('Refund already requested')}{x.refundStatus ? ` · ${t(x.refundStatus)}` : ''}
            </span>
          )
        }
        if (!x.bookingId) return <span className="text-xs text-on-surface-variant">{t('No booking to refund')}</span>
        return (
          <Button size="sm" variant="secondary" onClick={() => setRefunding(x)}>
            {t('Request refund')}
          </Button>
        )
      },
    },
  ]

  return (
    <Panel>
      <p className="mb-3 text-sm text-on-surface-variant">
        {t('Payments that arrived after their order expired, and gateway refunds still waiting for a result.')}
      </p>
      {checked && (
        <div className="mb-3">
          <Notice tone="success">
            {t('Refund status')}: {t(checked.status)}
          </Notice>
        </div>
      )}
      {checking.error && (
        <div className="mb-3">
          <Notice tone="error">{checking.error}</Notice>
        </div>
      )}
      <DataTable
        columns={columns}
        rows={data?.items ?? []}
        rowKey={(x) => `${x.kind}:${x.transactionId}`}
        empty={items.loading ? 'Loading…' : 'Nothing needs reconciling.'}
      />
      {data && (
        <Pagination
          pageNumber={pageNumber}
          totalPages={data.totalPages}
          busy={items.fetching}
          onChange={setPageNumber}
        />
      )}
      {refunding && (
        <Modal title="Refund a late payment" onClose={() => setRefunding(null)}>
          <LatePaymentRefundForm
            item={refunding}
            onCancel={() => setRefunding(null)}
            onCreated={(result) => {
              setRefunding(null)
              onRefundCreated(result)
              reload()
            }}
          />
        </Modal>
      )}
    </Panel>
  )
}
