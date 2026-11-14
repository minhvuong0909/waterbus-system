import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../../../hooks'
import { setBoatSeatActive, updateBoatSeat } from '../services/boatSeatService'
import type { BoatSeat, SeatClass } from '../types'
import FormActions from './FormActions'

const inRange = (value: string) => {
  const n = Number(value)
  return Number.isInteger(n) && n >= 1 && n <= 100
}

/**
 * Edits one seat or switches it off and on. A seat held or sold on a trip that has not ended
 * cannot be changed; the backend says so and the reason is shown here.
 */
export default function SeatForm({
  seat,
  seatClasses,
  onDone,
  onCancel,
}: {
  seat: BoatSeat
  seatClasses: SeatClass[]
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    seatCode: seat.seatCode,
    seatClassId: seat.seatClassId,
    rowNumber: String(seat.rowNumber),
    columnNumber: String(seat.columnNumber),
  })
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const errors = {
    seatCode: form.seatCode.trim() ? undefined : t('Code is required.'),
    rowNumber: inRange(form.rowNumber) ? undefined : t('Enter a number from 1 to {max}.', { max: 100 }),
    columnNumber: inRange(form.columnNumber) ? undefined : t('Enter a number from 1 to {max}.', { max: 100 }),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const saved = await run(() =>
      updateBoatSeat(seat.boatId, seat.id, {
        seatCode: form.seatCode.trim().toUpperCase(),
        seatClassId: form.seatClassId,
        rowNumber: Number(form.rowNumber),
        columnNumber: Number(form.columnNumber),
      }),
    )
    if (saved) onDone()
  }

  const toggle = async () => {
    if (await run(() => setBoatSeatActive(seat.boatId, seat.id, !seat.isActive))) onDone()
  }

  // The class of the seat stays selectable even after it was switched off.
  const options = seatClasses.some((c) => c.id === seat.seatClassId)
    ? seatClasses
    : [...seatClasses, { id: seat.seatClassId, name: seat.seatClassName }]

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <TextField label="Code" value={form.seatCode} onChange={set('seatCode')} error={shown.seatCode} maxLength={10} />
        <SelectField label="Seat class" value={form.seatClassId} onChange={set('seatClassId')}>
          {options.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </SelectField>
        <TextField label="Row" inputMode="numeric" value={form.rowNumber} onChange={set('rowNumber')} error={shown.rowNumber} />
        <TextField label="Column" inputMode="numeric" value={form.columnNumber} onChange={set('columnNumber')} error={shown.columnNumber} />
      </div>
      <div className="flex items-center justify-between gap-3 rounded-lg bg-mist px-3 py-2">
        <span className="text-sm text-on-surface-variant">
          {seat.isActive
            ? t('This seat is on sale for new trips.')
            : t('This seat is switched off and is not sold.')}
        </span>
        <Button size="sm" variant="neutral" disabled={busy} onClick={toggle}>
          {seat.isActive ? t('Deactivate') : t('Activate')}
        </Button>
      </div>
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
