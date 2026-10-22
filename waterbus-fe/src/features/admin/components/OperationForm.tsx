import { useState } from 'react'
import { t } from '../../../i18n'
import type { AdminTrip, OperationType } from '../types'
import { formatDateTime } from '../utils'
import { useSubmit } from '../hooks/useAdmin'
import { operateTrip } from '../services/adminService'
import { ActionButton, Notice } from './AdminUi'

export const OPERATION_LABEL: Record<OperationType, string> = {
  OpenBoarding: 'Open boarding',
  Complete: 'Close trip',
  NoAction: 'No action needed',
  Suspend: 'Suspend',
  Resume: 'Resume',
  Cancel: 'Cancel trip',
  Terminate: 'Terminate',
  Start: 'Start',
  Arrive: 'Arrive',
  SOS: 'SOS',
}

const OPERATION_HELP: Partial<Record<OperationType, string>> = {
  Suspend: 'The trip is put on hold until you resume it. Tickets stay valid and nothing is refunded.',
  Resume: 'The trip goes back to where it was.',
  Cancel: 'The trip will not run. Passengers see it as cancelled and may ask for a refund.',
  Terminate: 'Ends a trip that is already sailing, for an emergency. Passengers are refunded for the part not served.',
  Complete: 'Closes the trip after it has arrived.',
  NoAction: 'You looked at this alert and the trip needs nothing. The decision is kept in the log.',
}

/** Asks for the reason of a dispatch decision on a trip and sends it (`POST /trips/{id}/status`). */
export default function OperationForm({
  trip,
  type,
  by,
  incidentId,
  onCancel,
  onDone,
}: {
  trip: AdminTrip
  type: OperationType
  by: string
  /** The open alert this decision answers; the backend records it against that trip. */
  incidentId?: string
  onCancel: () => void
  onDone: () => void
}) {
  const [reason, setReason] = useState('')
  const [showError, setShowError] = useState(false)
  const { run, busy, error } = useSubmit()

  return (
    <form
      noValidate
      className="space-y-3"
      onSubmit={async (e) => {
        e.preventDefault()
        if (!reason.trim()) {
          setShowError(true)
          return
        }
        const done = await run(() => operateTrip(trip.id, type, reason.trim(), by, incidentId))
        if (done) onDone()
      }}
    >
      <p className="text-sm text-on-surface-variant">
        {t('Trip of {time}.', { time: formatDateTime(trip.departureTime) })} {t(OPERATION_HELP[type] ?? '')}
      </p>
      <div>
        <label htmlFor="operation-reason" className="mb-1 block text-xs font-semibold text-on-surface-variant">
          {t('Reason')}
        </label>
        <textarea
          id="operation-reason"
          rows={3}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          className="w-full rounded-lg border border-outline-variant px-3 py-2 text-sm text-deep-river focus:border-teal-flow focus:ring-teal-flow"
          aria-invalid={showError && !reason.trim()}
        />
        {showError && !reason.trim() && (
          <p role="alert" className="mt-1 text-[11px] text-coral-glow">
            {t('Give a reason, it is kept in the log.')}
          </p>
        )}
      </div>
      {error && <Notice tone="error">{error}</Notice>}
      <div className="flex justify-end gap-2 pt-1">
        <ActionButton tone="secondary" onClick={onCancel}>
          {t('Back')}
        </ActionButton>
        <ActionButton type="submit" tone={type === 'Cancel' || type === 'Terminate' ? 'danger' : 'primary'} disabled={busy}>
          {busy ? t('Working…') : t(OPERATION_LABEL[type])}
        </ActionButton>
      </div>
    </form>
  )
}
