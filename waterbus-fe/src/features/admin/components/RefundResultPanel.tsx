import { CopyButton, Panel } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import type { RefundRequestResult } from '../types'
import ManualRefundForm from './ManualRefundForm'

/** Lệnh hoàn vừa tạo: số tiền theo từng vé, và form hoàn tay khi cổng thanh toán không tự hoàn được. */
export default function RefundResultPanel({ refund }: { refund: RefundRequestResult }) {
  return (
    <Panel title="Refund created" className="mt-4">
      <dl className="grid gap-3 text-sm sm:grid-cols-4">
        <Fact label="Amount" value={formatVnd(refund.amount)} />
        <Fact label="Status" value={t(refund.status)} />
        <Fact label="Gateway" value={refund.gateway} />
        <div>
          <dt className="text-2xs font-semibold uppercase tracking-wide text-on-surface-variant">{t('Refund ID')}</dt>
          <dd className="mt-1 flex items-center gap-1 font-mono text-xs">
            <span className="truncate">{refund.refundTransactionId}</span>
            <CopyButton value={refund.refundTransactionId} />
          </dd>
        </div>
      </dl>
      {refund.tickets.length > 0 && (
        <ul className="mt-4 divide-y divide-outline-variant/30 rounded-xl border border-outline-variant/30">
          {refund.tickets.map((ticket) => (
            <li key={ticket.ticketId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
              <span className="font-mono font-semibold">{ticket.ticketCode}</span>
              <span className="text-on-surface-variant">{ticket.reason}</span>
              <span className="font-semibold">{formatVnd(ticket.amount)}</span>
            </li>
          ))}
        </ul>
      )}
      {refund.isDemo && !refund.requiresManualAction && (
        <p className="mt-4 text-sm font-semibold text-deep-river">{refund.notice ?? t('DEMO: no real money is transferred.')}</p>
      )}
      {refund.requiresManualAction && <ManualRefundForm refund={refund} />}
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
