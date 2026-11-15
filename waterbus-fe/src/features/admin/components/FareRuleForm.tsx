import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField, TextField, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import type { TripType } from '../../trips/types'
import { fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import { useSubmit } from '../../../hooks'
import { createFareRule, updateFareRule } from '../services/fareRuleService'
import type { FareRule, SeatClass } from '../types'
import FormActions from './FormActions'

/** Giá cao nhất backend nhận (`decimal(12,2)`). */
const MAX_PRICE = 9_999_999_999.99

/**
 * Thêm hoặc sửa một giá (BR-FARE-01: một giá cho mỗi loại chuyến và hạng ghế). Giá đã có hiệu lực giữ nguyên số
 * tiền và ngày bắt đầu: chỉ được đưa ngày kết thúc sớm hơn, giá mới là một quy tắc giá mới.
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
  // Giá đã có hiệu lực: backend chỉ cho đưa ngày kết thúc SỚM hơn (không kéo dài, không bỏ).
  const originalEnd = rule?.effectiveTo ? toVietnamDateTimeInput(rule.effectiveTo) : ''
  const endError =
    inEffect && originalEnd && (!form.to || form.to > originalEnd)
      ? t('This fare is in effect: its end can only be brought earlier.')
      : !form.to || !form.from || form.to > form.from
        ? undefined
        : t('The end must be after the start.')
  const errors = {
    seatClassId: form.seatClassId ? undefined : t('Choose a seat class.'),
    price:
      form.price.trim() !== '' && price >= 0 && price <= MAX_PRICE && Math.abs(price * 100 - Math.round(price * 100)) < 1e-6
        ? undefined
        : t('Enter a price in VND, with at most 2 decimals.'),
    from: form.from ? undefined : t('Choose when the fare starts.'),
    to: endError,
  }
  const shown: Partial<typeof errors> = showErrors ? errors : {}

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.values(errors).some(Boolean)) {
      setShowErrors(true)
      return
    }
    // Ô nhập chỉ tới phút còn mốc gốc có thể có giây; trường không đổi thì gửi lại đúng giá trị gốc, kẻo backend
    // coi giá đã có hiệu lực là bị sửa ngày bắt đầu.
    const startUnchanged = rule !== undefined && form.from === toVietnamDateTimeInput(rule.effectiveFrom)
    const endUnchanged = rule !== undefined && form.to === originalEnd
    const times = {
      price,
      effectiveFrom: startUnchanged ? rule.effectiveFrom : fromVietnamDateTimeInput(form.from),
      effectiveTo: endUnchanged ? (rule.effectiveTo ?? null) : form.to ? fromVietnamDateTimeInput(form.to) : null,
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
          hint={inEffect && originalEnd ? 'Can only be brought earlier.' : 'Leave empty for no end.'}
          type="datetime-local"
          value={form.to}
          onChange={set('to')}
          error={shown.to}
        />
      </div>
      {inEffect && (
        <Notice tone="info">
          {t('This fare is already in effect: only its end can be brought forward. To change the price, add a new fare.')}
        </Notice>
      )}
      <FormActions error={error} busy={busy} onCancel={onCancel} />
    </form>
  )
}
