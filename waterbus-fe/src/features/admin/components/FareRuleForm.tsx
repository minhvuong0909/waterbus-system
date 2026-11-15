import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField, TextField, Notice } from '../../../components/ui'
import { t } from '../../../i18n'
import type { TripType } from '../../trips/types'
import { fromVietnamDateTimeInput, toVietnamDateTimeInput } from '../../../utils/time'
import { useSubmit } from '../../../hooks'
import { hasSaneYear, parseWhole } from '../../../utils/inputRules'
import { createFareRule, updateFareRule } from '../services/fareRuleService'
import type { FareRule, SeatClass } from '../types'
import FormActions from './FormActions'

/** Giá cao nhất backend nhận (`decimal(12,2)`). */
const MAX_PRICE = 9_999_999_999

/**
 * Thêm hoặc sửa một giá (BR-FARE-01: một giá cho mỗi loại chuyến và hạng ghế). Giá đã có hiệu lực giữ nguyên số
 * tiền và ngày bắt đầu: chỉ được đưa ngày kết thúc sớm hơn, giá mới là một quy tắc giá mới.
 */
export default function FareRuleForm({
  rule,
  rules,
  seatClasses,
  onDone,
  onCancel,
}: {
  rule?: FareRule
  /** Mọi bảng giá hiện có: BR-FARE-01 chỉ một giá cho mỗi loại chuyến + hạng ghế tại một thời điểm. */
  rules: FareRule[]
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
  // Tiền Việt không có đơn vị lẻ và giá 0 đồng không thanh toán được qua cổng.
  const price = parseWhole(form.price, 1, MAX_PRICE)
  // Giá đã có hiệu lực: backend chỉ cho đưa ngày kết thúc SỚM hơn (không kéo dài, không bỏ).
  const originalEnd = rule?.effectiveTo ? toVietnamDateTimeInput(rule.effectiveTo) : ''
  const endError =
    inEffect && originalEnd && (!form.to || form.to > originalEnd)
      ? t('This fare is in effect: its end can only be brought earlier.')
      : !form.to
        ? undefined
        : !hasSaneYear(form.to)
          ? t('Choose a valid date.')
          : !form.from || form.to > form.from
            ? undefined
        : t('The end must be after the start.')
  const start = form.from ? Date.parse(fromVietnamDateTimeInput(form.from)) : NaN
  const end = form.to ? Date.parse(fromVietnamDateTimeInput(form.to)) : Infinity
  const overlaps =
    !Number.isNaN(start) &&
    rules.some(
      (other) =>
        other.id !== rule?.id &&
        other.isActive &&
        other.tripType === form.tripType &&
        other.seatClassId === form.seatClassId &&
        start < (other.effectiveTo ? Date.parse(other.effectiveTo) : Infinity) &&
        Date.parse(other.effectiveFrom) < end,
    )
  const errors = {
    seatClassId: form.seatClassId ? undefined : t('Choose a seat class.'),
    price: price !== null ? undefined : t('Enter a price in VND: a whole number greater than 0.'),
    overlap: overlaps ? t('Another fare for this trip type and seat class is already in effect during this period.') : undefined,
    from: hasSaneYear(form.from) ? undefined : t('Choose when the fare starts.'),
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
      price: price as number,
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
        <SelectField label="Trip type" required value={form.tripType} onChange={set('tripType')} disabled={Boolean(rule)}>
          <option value="Commuter">{t('Regular')}</option>
          <option value="Sightseeing">{t('Sightseeing')}</option>
        </SelectField>
        <SelectField
          label="Seat class"
          required
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
        required
        inputMode="decimal"
        value={form.price}
        onChange={set('price')}
        error={shown.price}
        disabled={inEffect}
      />
      <div className="grid grid-cols-2 gap-3">
        <TextField
          label="Starts"
          required
          type="datetime-local"
          value={form.from}
          onChange={set('from')}
          error={shown.from ?? shown.overlap}
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
