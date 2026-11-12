import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import type { TripType } from '../../trips/types'
import { fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import { useSubmit } from '../hooks/useAdmin'
import { createFareRule, updateFareRule } from '../services/fareRuleService'
import type { FareRule, SeatClass } from '../types'
import FormActions from './FormActions'

/** Highest fare the backend accepts (`decimal(12,2)`). */
const MAX_PRICE = 9_999_999_999.99

/**
 * Adds a fare or edits one (BR-FARE-01: one price per trip type and seat class). A fare that has
 * already taken effect keeps its price and start: only its end can be brought forward, and a new
 * price is a new fare.
 */
export default function FareRuleForm({
  rule,
  seatClasses,
  onDone,
  onCancel,
}: {
  rule?: FareRule
  seatClasses: SeatClass[]
  onDone: () => void
  onCancel: () => void
}) {
  const [form, setForm] = useState({
    tripType: rule?.tripType ?? ('Commuter' as TripType),
    seatClassId: rule?.seatClassId ?? seatClasses[0]?.id ?? '',
    price: rule ? String(rule.price) : '',
    from: rule ? toVietnamDateTimeInput(rule.effectiveFrom) : '',
    to: rule?.effectiveTo ? toVietnamDateTimeInput(rule.effectiveTo) : '',
  })
  const [showErrors, setShowErrors] = useState(false)
  const { run, busy, error } = useSubmit()
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const inEffect = rule !== undefined && rule.status !== 'Upcoming'
  const price = Number(form.price)
  const errors = {
    seatClassId: form.seatClassId ? undefined : t('Choose a seat class.'),
    price:
      form.price.trim() !== '' && price >= 0 && price <= MAX_PRICE
        ? undefined
        : t('Enter a price in VND.'),
    from: form.from ? undefined : t('Choose when the fare starts.'),
    to: !form.to || !form.from || form.to > form.from ? undefined : t('The end must be after the start.'),
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    const times = {
      price,
      effectiveFrom: fromVietnamDateTimeInput(form.from),
      effectiveTo: form.to ? fromVietnamDateTimeInput(form.to) : null,
    }
    const saved = await run(async () => {
      if (rule) await updateFareRule(rule.id, times)
      else await createFareRule({ tripType: form.tripType, seatClassId: form.seatClassId, ...times })
      return true
    })
    if (saved) onDone()
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <SelectField label="Trip type" value={form.tripType} onChange={set('tripType')} disabled={Boolean(rule)}>
          <option value="Commuter">{t('Regular')}</option>
          <option value="Sightseeing">{t('Sightseeing')}</option>
        </SelectField>
        <SelectField
          label="Seat class"
          value={form.seatClassId}
          onChange={set('seatClassId')}
          disabled={Boolean(rule)}
          error={shown.seatClassId}
        >
          {rule ? (
            <option value={rule.seatClassId}>{rule.seatClassName}</option>
          ) : (
            seatClasses.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))
          )}
        </SelectField>
      </div>
      <TextField
        label="Price (VND)"
        inputMode="decimal"
        value={form.price}
        onChange={set('price')}
        error={shown.price}
        disabled={inEffect}
      />
      <div className="grid grid-cols-2 gap-3">
        <TextField
          label="Starts"
          type="datetime-local"
          value={form.from}
          onChange={set('from')}
          error={shown.from}
          disabled={inEffect}
        />
        <TextField
          label="Ends"
          hint="Leave empty for no end."
          type="datetime-local"
          value={form.to}
          onChange={set('to')}
          error={shown.to}
        />
      </div>
      {inEffect && (
        <p className="rounded-lg bg-mist px-3 py-2 text-sm text-on-surface-variant">
          {t('This fare is already in effect: only its end can be brought forward. To change the price, add a new fare.')}
        </p>
      )}
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
