import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, Modal, Notice, PageLoader, TextField } from '../../../components/ui'
import { useFetch, useSubmit } from '../../../hooks'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import { getTripAttendance, recordTripAttendance } from '../services/personnelService'
import type { AttendanceState, TripAttendance } from '../types/personnel'
import FormActions from './FormActions'
import StatusBadge from './StatusBadge'
import type { BadgeTone } from './StatusBadge'

const STATE: Record<AttendanceState, { label: string; tone: BadgeTone; hint: string }> = {
  Pending: { label: 'Not yet due', tone: 'muted', hint: 'The Captain is not overdue yet. Record attendance once they are at the pier.' },
  Missing: { label: 'Overdue', tone: 'upcoming', hint: 'The Captain has not been confirmed present and the deadline has passed.' },
  Present: { label: 'Present', tone: 'active', hint: 'Attendance was recorded on time.' },
  Late: { label: 'Present (late)', tone: 'upcoming', hint: 'Attendance was recorded after the deadline.' },
  NotConfigured: { label: 'Not configured', tone: 'muted', hint: 'The check-in window of the first stop is not set, so no deadline can be calculated.' },
}

function RecordForm({ tripId, attendance, onDone, onCancel }: {
  tripId: string
  attendance: TripAttendance
  onDone: () => void
  onCancel: () => void
}) {
  const [reason, setReason] = useState('')
  const [showError, setShowError] = useState(false)
  const { run, busy, error } = useSubmit()
  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!reason.trim()) return setShowError(true)
    // `assignmentVersion` lấy từ lần đọc này: nếu Admin đổi Captain/tàu giữa chừng, server từ chối thay vì xác nhận nhầm người.
    const saved = await run(async () => {
      await recordTripAttendance(tripId, { captainId: attendance.captainId!, assignmentVersion: attendance.assignmentVersion!, reason })
      return true
    })
    if (saved) onDone()
  }
  return (
    <form onSubmit={submit} noValidate className="mt-3 space-y-3">
      <TextField label="Reason" required value={reason} maxLength={1000} onChange={(e) => setReason(e.target.value)}
        error={showError && !reason.trim() ? t('Enter a reason.') : undefined}
        hint="For example: seen at the pier by the Admin." />
      <FormActions error={error} busy={busy} submitLabel="Record attendance" onCancel={onCancel} />
    </form>
  )
}

/** Admin ghi nhận Captain đã có mặt cho một chuyến (BR-CAP-09/11/12); Captain không tự điểm danh. */
export default function CaptainAttendanceDialog({ tripId, tripLabel, captainNames, onDone, onClose }: {
  tripId: string
  tripLabel: string
  captainNames: Map<string, string>
  onDone: () => void
  onClose: () => void
}) {
  const loaded = useFetch(() => getTripAttendance(tripId), [tripId])
  const data = loaded.data

  return (
    <Modal title="Captain attendance" onClose={onClose}>
      <p className="mb-3 text-sm text-on-surface-variant">{tripLabel}</p>
      {loaded.error ? (
        <ErrorState compact onRetry={loaded.retry} />
      ) : loaded.loading || !data ? (
        <PageLoader />
      ) : !data.captainId ? (
        <Notice tone="warning">{t('This trip has no Captain. Assign a Captain to the boat first.')}</Notice>
      ) : (
        <div className="space-y-3 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <span className="font-semibold text-deep-river">{captainNames.get(data.captainId) ?? t('Captain')}</span>
            <StatusBadge tone={STATE[data.state].tone} label={STATE[data.state].label} />
          </div>
          <p className="text-on-surface-variant">{t(STATE[data.state].hint)}</p>
          {data.requiredAt && (
            <p>{t('Required by {time}', { time: formatDateTime(data.requiredAt) })}</p>
          )}
          {data.attendance ? (
            <div className="rounded-xl bg-mist/70 p-3">
              <p>{t('Recorded {time}', { time: formatDateTime(data.attendance.recordedAt) })}</p>
              <p className="text-on-surface-variant">{data.attendance.reason}</p>
            </div>
          ) : (
            data.state !== 'NotConfigured' && (
              <RecordForm tripId={tripId} attendance={data} onCancel={onClose}
                onDone={onDone} />
            )
          )}
        </div>
      )}
    </Modal>
  )
}
