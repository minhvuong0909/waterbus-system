import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, Notice, Panel, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { isGuid } from '../../../utils/guid'
import { useSubmit } from '../../../hooks'
import { requestRefund } from '../services/refundService'
import type { RefundRequestResult } from '../types'

/** Tạo lệnh hoàn tiền cho một Booking (BR-RF-07); kết quả được trả lên trang qua `onCreated`. */
export default function RefundRequestForm({ onCreated }: { onCreated: (result: RefundRequestResult) => void }) {
  const [bookingId, setBookingId] = useState('')
  const [reason, setReason] = useState('')
  const { run, busy, error } = useSubmit()

  const id = bookingId.trim()
  const validId = isGuid(id)
  const canSubmit = validId && reason.trim().length > 0
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!canSubmit) return
    const created = await run(() => requestRefund(id, reason.trim()))
    if (created) onCreated(created)
  }

  return (
    <Panel title="New refund">
      <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
        <TextField
          label="Booking ID"
          hint="The booking's ID (GUID), from the order or the customer's request."
          value={bookingId}
          onChange={(e) => setBookingId(e.target.value)}
          error={id && !validId ? t('Enter a valid booking ID.') : undefined}
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
          <Button type="submit" icon="receipt_long" disabled={busy || !canSubmit}>
            {busy ? t('Sending…') : t('Request refund')}
          </Button>
          {error && <Notice tone="error">{error}</Notice>}
        </div>
      </form>
    </Panel>
  )
}
