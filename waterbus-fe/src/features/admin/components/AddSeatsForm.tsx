import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { useSubmit } from '../../../hooks'
import { planSeatLayout } from '../utils/seatBatch'
import type { ExistingSeat } from '../utils/seatBatch'
import { addBoatSeats } from '../services/boatSeatService'
import type { SeatClass } from '../types'
import FormActions from './FormActions'

/** The backend takes at most this many seats in one request. */
const MAX_BATCH = 200

const wholeNumber = (value: string, min: number, max: number) => {
  const n = Number(value)
  return value.trim() !== '' && Number.isInteger(n) && n >= min && n <= max
}

interface BlockForm {
  key: number
  seatClassId: string
  prefix: string
  count: string
}

/**
 * Adds the seats of a whole boat in one request: one line per cabin (seat class, code prefix, how many).
 * Numbers and rows continue after the seats the boat already has, so only the class, the prefix and the
 * count are typed. `room` is how many more active seats the boat can take (its total minus the active ones).
 */
export default function AddSeatsForm({
  boatId,
  seatClasses,
  existingSeats,
  room,
  onDone,
  onCancel,
}: {
  boatId: string
  seatClasses: SeatClass[]
  existingSeats: readonly ExistingSeat[]
  room: number
  onDone: () => void
  onCancel: () => void
}) {
  const maxTotal = Math.min(room, MAX_BATCH)
  const [blocks, setBlocks] = useState<BlockForm[]>([
    { key: 0, seatClassId: seatClasses[0]?.id ?? '', prefix: '', count: String(maxTotal) },
  ])
  const [seatsPerRow, setSeatsPerRow] = useState('4')
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()

  const counts = blocks.map((b) => (wholeNumber(b.count, 1, maxTotal) ? Number(b.count) : 0))
  const total = counts.reduce((sum, n) => sum + n, 0)
  const blockErrors = blocks.map((b, i) => ({
    prefix: /^[A-Za-z]{1,6}$/.test(b.prefix.trim()) ? undefined : t('Use 1 to 6 letters.'),
    count: counts[i] > 0 ? undefined : t('Enter a number from 1 to {max}.', { max: maxTotal }),
  }))
  const rowError = wholeNumber(seatsPerRow, 1, 100) ? undefined : t('Enter a number from 1 to {max}.', { max: 100 })
  const totalError = total > maxTotal ? t('You can add at most {max} seats now.', { max: maxTotal }) : undefined
  const valid =
    blocks.every((b) => b.seatClassId !== '') &&
    blockErrors.every((e) => !e.prefix && !e.count) &&
    !rowError &&
    !totalError

  const plan = valid
    ? planSeatLayout(
        blocks.map((b, i) => ({ seatClassId: b.seatClassId, prefix: b.prefix, count: counts[i] })),
        existingSeats,
        Number(seatsPerRow),
      )
    : null

  const update = (key: number, change: Partial<BlockForm>) =>
    setBlocks((current) => current.map((b) => (b.key === key ? { ...b, ...change } : b)))
  const addBlock = () =>
    setBlocks((current) => {
      const used = current.reduce((sum, b) => sum + (wholeNumber(b.count, 1, maxTotal) ? Number(b.count) : 0), 0)
      const unused = seatClasses.find((c) => !current.some((b) => b.seatClassId === c.id)) ?? seatClasses[0]
      return [
        ...current,
        { key: Math.max(...current.map((b) => b.key)) + 1, seatClassId: unused?.id ?? '', prefix: '', count: String(Math.max(maxTotal - used, 1)) },
      ]
    })
  const removeBlock = (key: number) => setBlocks((current) => current.filter((b) => b.key !== key))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!plan) {
      setShowErrors(true)
      return
    }
    if (await run(() => addBoatSeats(boatId, plan.seats))) onDone()
  }

  const summary = plan?.blocks.map((b) => (b.count === 1 ? b.first : `${b.first}–${b.last}`)).join(', ')

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      {blocks.map((b, i) => (
        <div key={b.key} className="grid grid-cols-[1fr_5rem_5rem_auto] items-start gap-2">
          <SelectField label="Seat class" value={b.seatClassId} onChange={(e) => update(b.key, { seatClassId: e.target.value })}>
            {seatClasses.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </SelectField>
          <TextField
            label="Code prefix"
            placeholder="S"
            value={b.prefix}
            onChange={(e) => update(b.key, { prefix: e.target.value })}
            error={showErrors ? blockErrors[i].prefix : undefined}
          />
          <TextField
            label="Number of seats"
            inputMode="numeric"
            value={b.count}
            onChange={(e) => update(b.key, { count: e.target.value })}
            error={showErrors ? blockErrors[i].count : undefined}
          />
          {blocks.length > 1 && (
            <Button size="sm" variant="text" icon="close" className="mt-6" aria-label={t('Remove')} onClick={() => removeBlock(b.key)}>
              {''}
            </Button>
          )}
        </div>
      ))}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <Button size="sm" variant="neutral" icon="add" onClick={addBlock} disabled={blocks.length >= seatClasses.length * 2}>
          {t('Add another class')}
        </Button>
        <div className="w-40">
          <TextField
            label="Seats per row"
            inputMode="numeric"
            value={seatsPerRow}
            onChange={(e) => setSeatsPerRow(e.target.value)}
            error={showErrors ? rowError : undefined}
          />
        </div>
      </div>
      <p className="rounded-lg bg-mist px-3 py-2 text-sm text-on-surface-variant">
        {plan
          ? t('Adds {count} seats ({summary}).', { count: total, summary: summary ?? '' })
          : (showErrors && totalError) || t('This boat has room for {count} more active seats.', { count: room })}
      </p>
      <FormActions error={error} busy={busy} submitLabel="Add seats" onCancel={onCancel} />
    </form>
  )
}
