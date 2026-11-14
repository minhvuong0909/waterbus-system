import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../../../hooks'
import { buildSeatBatch, seatCode } from '../utils/seatBatch'
import { addBoatSeats } from '../services/boatSeatService'
import type { SeatClass } from '../types'
import FormActions from './FormActions'

/** The backend takes at most this many seats in one request. */
const MAX_BATCH = 200

const wholeNumber = (value: string, min: number, max: number) => {
  const n = Number(value)
  return value.trim() !== '' && Number.isInteger(n) && n >= min && n <= max
}

/**
 * Adds a run of seats of one class, numbered in sequence and laid out row by row. `room` is how
 * many more active seats the boat can take (its total seats minus the active ones).
 */
export default function AddSeatsForm({
  boatId,
  seatClasses,
  room,
  onDone,
  onCancel,
}: {
  boatId: string
  seatClasses: SeatClass[]
  room: number
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    seatClassId: seatClasses[0]?.id ?? '',
    prefix: '',
    firstNumber: '1',
    count: String(Math.min(room, 4)),
    firstRow: '1',
    seatsPerRow: '4',
  })
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const maxCount = Math.min(room, MAX_BATCH)
  const errors = {
    prefix: /^[A-Za-z]{1,6}$/.test(form.prefix.trim()) ? undefined : t('Use 1 to 6 letters.'),
    firstNumber: wholeNumber(form.firstNumber, 1, 9999) ? undefined : t('Enter a whole number from 1.'),
    count: wholeNumber(form.count, 1, maxCount)
      ? undefined
      : t('Enter a number from 1 to {max}.', { max: maxCount }),
    firstRow: wholeNumber(form.firstRow, 1, 100) ? undefined : t('Enter a number from 1 to {max}.', { max: 100 }),
    seatsPerRow: wholeNumber(form.seatsPerRow, 1, 100)
      ? undefined
      : t('Enter a number from 1 to {max}.', { max: 100 }),
  }
  const valid = Object.values(errors).every((x) => !x) && form.seatClassId !== ''
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const prefix = form.prefix.trim().toUpperCase()
  const first = Number(form.firstNumber)
  const count = Number(form.count)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!valid) {
      setShowErrors(true)
      return
    }
    const seats = buildSeatBatch({
      seatClassId: form.seatClassId,
      prefix,
      firstNumber: first,
      count,
      firstRow: Number(form.firstRow),
      seatsPerRow: Number(form.seatsPerRow),
    })
    if (await run(() => addBoatSeats(boatId, seats))) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <SelectField label="Seat class" value={form.seatClassId} onChange={set('seatClassId')}>
        {seatClasses.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </SelectField>
      <div className="grid grid-cols-3 gap-3">
        <TextField label="Code prefix" placeholder="S" value={form.prefix} onChange={set('prefix')} error={shown.prefix} />
        <TextField label="First number" inputMode="numeric" value={form.firstNumber} onChange={set('firstNumber')} error={shown.firstNumber} />
        <TextField label="Number of seats" inputMode="numeric" value={form.count} onChange={set('count')} error={shown.count} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <TextField label="First row" inputMode="numeric" value={form.firstRow} onChange={set('firstRow')} error={shown.firstRow} />
        <TextField label="Seats per row" inputMode="numeric" value={form.seatsPerRow} onChange={set('seatsPerRow')} error={shown.seatsPerRow} />
      </div>
      <p className="rounded-lg bg-mist px-3 py-2 text-sm text-on-surface-variant">
        {valid
          ? count === 1
            ? t('Adds seat {code}.', { code: seatCode(prefix, first) })
            : t('Adds seats {first} to {last}.', {
                first: seatCode(prefix, first),
                last: seatCode(prefix, first + count - 1),
              })
          : t('This boat has room for {count} more active seats.', { count: room })}
      </p>
      <FormActions error={error} busy={busy} submitLabel="Add seats" onCancel={onCancel} />
    </form>
  )
}
