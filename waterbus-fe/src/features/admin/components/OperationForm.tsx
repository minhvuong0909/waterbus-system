import { useState } from 'react'
import { t } from '../../../i18n'
import { OPERATION_LABEL } from '../../operations/utils'
import type { AdminTrip, OperationType } from '../../operations/types'
import { formatDateTime } from '../../../utils/time'
import { errorText } from '../../../hooks/useSubmit'
import { operateTrip } from '../../operations/services/operationsService'
import { Button, Notice } from '../../../components/ui'
import { INPUT_CLASS, REQUIRED_LABEL_CLASS } from '../../../components/ui/styles'

/** Thao tác backend bắt buộc có lý do (`TripLifecycle.RequiresReason`); các thao tác khác để trống được. */
const REASON_REQUIRED: OperationType[] = ['Suspend', 'Cancel', 'Terminate', 'NoAction']

const OPERATION_HELP: Partial<Record<OperationType, string>> = {
  Suspend: 'The trip is put on hold until you resume it. Tickets stay valid and nothing is refunded.',
  Resume: 'The trip goes back to where it was.',
  Cancel: 'The trip will not run. Passengers see it as cancelled and may ask for a refund.',
  Terminate: 'Ends a trip that is already sailing, for an emergency. Passengers are refunded for the part not served.',
  Complete: 'Closes the trip after it has arrived.',
  NoAction: 'You looked at this alert and the trip needs nothing. The decision is kept in the log.',
}

interface Failure {
  trip: AdminTrip
  message: string
}

/**
 * Hỏi lý do rồi gửi quyết định của Admin cho một hoặc nhiều chuyến (`POST /trips/{id}/status`, lần lượt từng chuyến).
 * Chuyến nào bị từ chối được liệt kê kèm lý do của server; các chuyến đã xử lý xong vẫn giữ kết quả.
 */
export default function OperationForm({
  trips,
  type,
  incidentId,
  onCancel,
  onDone,
}: {
  trips: AdminTrip[]
  type: OperationType
  /** Sự cố mà quyết định này trả lời; backend ghi quyết định vào liên kết sự cố - chuyến. */
  incidentId?: string
  onCancel: () => void
  onDone: () => void
}) {
  const [reason, setReason] = useState('')
  const [showError, setShowError] = useState(false)
  const [busy, setBusy] = useState(false)
  const [failures, setFailures] = useState<Failure[] | null>(null)
  const destructive = type === 'Cancel' || type === 'Terminate'
  const single = trips.length === 1
  const reasonRequired = REASON_REQUIRED.includes(type)

  const submit = async () => {
    if (reasonRequired && !reason.trim()) {
      setShowError(true)
      return
    }
    setBusy(true)
    const failed: Failure[] = []
    // Lần lượt từng chuyến: mỗi lần đổi trạng thái là một TripOperationEvent riêng ở backend.
    for (const trip of trips) {
      try {
        await operateTrip(trip.id, type, reason.trim(), incidentId)
      } catch (error) {
        failed.push({ trip, message: errorText(error) })
      }
    }
    setBusy(false)
    if (failed.length === 0) onDone()
    else setFailures(failed)
  }

  if (failures) {
    const done = trips.length - failures.length
    return (
      <div className="space-y-3">
        {done > 0 && <Notice tone="success">{t('Done for {count} trips.', { count: done })}</Notice>}
        <Notice tone="error">{t('{count} trips were not changed:', { count: failures.length })}</Notice>
        <ul className="divide-y divide-outline-variant/30 rounded-xl border border-outline-variant/30 text-sm">
          {failures.map(({ trip, message }) => (
            <li key={trip.id} className="px-3 py-2">
              <div className="font-semibold text-deep-river">
                <span className="font-mono">{trip.boatCode ?? '—'}</span> · {formatDateTime(trip.departureTime)}
              </div>
              <div className="text-on-surface-variant">{message}</div>
            </li>
          ))}
        </ul>
        <div className="flex justify-end">
          <Button size="sm" onClick={onDone}>
            {t('Close')}
          </Button>
        </div>
      </div>
    )
  }

  return (
    <form
      noValidate
      className="space-y-3"
      onSubmit={(e) => {
        e.preventDefault()
        void submit()
      }}
    >
      <p className="text-sm text-on-surface-variant">
        {single
          ? t('Trip of {time}.', { time: formatDateTime(trips[0].departureTime) })
          : t('{count} trips selected.', { count: trips.length })}{' '}
        {t(OPERATION_HELP[type] ?? '')}
      </p>
      {!single && (
        <ul className="max-h-40 divide-y divide-outline-variant/30 overflow-y-auto rounded-xl border border-outline-variant/30 text-sm">
          {trips.map((trip) => (
            <li key={trip.id} className="flex items-center justify-between gap-3 px-3 py-1.5">
              <span className="font-mono font-semibold text-deep-river">{trip.boatCode ?? '—'}</span>
              <span className="text-on-surface-variant">{formatDateTime(trip.departureTime)}</span>
            </li>
          ))}
        </ul>
      )}
      <div>
        <label htmlFor="operation-reason" className={`mb-1 block text-xs font-semibold text-on-surface-variant ${reasonRequired ? REQUIRED_LABEL_CLASS : ''}`}>
          {t('Reason')}
        </label>
        <textarea
          id="operation-reason"
          rows={3}
          maxLength={500}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className={INPUT_CLASS}
          aria-required={reasonRequired || undefined}
          aria-invalid={showError && reasonRequired && !reason.trim()}
        />
        {showError && reasonRequired && !reason.trim() && (
          <p role="alert" className="mt-1 text-2xs text-coral-glow">
            {t('Give a reason, it is kept in the log.')}
          </p>
        )}
      </div>
      <div className="flex justify-end gap-2 pt-1">
        <Button size="sm" variant="neutral" onClick={onCancel} disabled={busy}>
          {t('Back')}
        </Button>
        <Button size="sm" type="submit" variant={destructive ? 'danger' : 'primary'} disabled={busy}>
          {busy
            ? t('Working…')
            : single
              ? t(OPERATION_LABEL[type])
              : t('{action} for {count} trips', { action: t(OPERATION_LABEL[type]), count: trips.length })}
        </Button>
      </div>
    </form>
  )
}
