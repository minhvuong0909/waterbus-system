import { useState } from 'react'
import type { FormEvent } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import {
  ActionButton,
  Notice,
  PageHeader,
  Panel,
  TextField,
} from '../../features/admin/components/AdminUi'
import { useSubmit } from '../../features/admin/hooks/useAdmin'
import { getFares, saveFares } from '../../features/admin/services/adminService'
import type { FareSettings } from '../../features/admin/types'
import { useDocumentTitle, useFetch } from '../../hooks'
import { formatNumber } from '../../utils/format'
import { t } from '../../i18n'

interface FormState {
  commuter: string
  sightseeing: string
  front: string
  standard: string
  outdoor: string
}

const toForm = (f: FareSettings): FormState => ({
  commuter: String(f.basePrice.Commuter),
  sightseeing: String(f.basePrice.Sightseeing),
  front: String(f.cabinMultiplier.front),
  standard: String(f.cabinMultiplier.standard),
  outdoor: String(f.cabinMultiplier.outdoor),
})

function validate(form: FormState): Partial<Record<keyof FormState, string>> {
  const errors: Partial<Record<keyof FormState, string>> = {}
  for (const key of ['commuter', 'sightseeing'] as const) {
    const price = Number(form[key])
    if (!Number.isInteger(price) || price <= 0) errors[key] = t('Enter a price in VND above 0.')
  }
  for (const key of ['front', 'standard', 'outdoor'] as const) {
    const multiplier = Number(form[key])
    if (!(multiplier >= 1 && multiplier <= 5)) errors[key] = t('Enter a multiplier from 1 to 5.')
  }
  return errors
}

function FaresForm({ fares }: { fares: FareSettings }) {
  const [form, setForm] = useState(() => toForm(fares))
  const [showErrors, setShowErrors] = useState(false)
  const [saved, setSaved] = useState(false)
  const { run, busy, error } = useSubmit()
  const errors = showErrors ? validate(form) : {}
  const set = (key: keyof FormState) => (e: { target: { value: string } }) => {
    setSaved(false)
    setForm((f) => ({ ...f, [key]: e.target.value }))
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (Object.keys(validate(form)).length > 0) {
      setShowErrors(true)
      return
    }
    const result = await run(() =>
      saveFares({
        basePrice: { Commuter: Number(form.commuter), Sightseeing: Number(form.sightseeing) },
        cabinMultiplier: {
          front: Number(form.front),
          standard: Number(form.standard),
          outdoor: Number(form.outdoor),
        },
      }),
    )
    if (result) setSaved(true)
  }

  const preview = (base: string, multiplier: string) =>
    formatNumber(Math.round((Number(base) || 0) * (Number(multiplier) || 0)))

  return (
    <form onSubmit={submit} noValidate className="grid gap-4 xl:grid-cols-2">
      <Panel title="Base price per trip type">
        <div className="grid grid-cols-2 gap-3">
          <TextField label="Regular (Commuter), VND" inputMode="numeric" value={form.commuter} onChange={set('commuter')} error={errors.commuter} />
          <TextField label="Sightseeing, VND" inputMode="numeric" value={form.sightseeing} onChange={set('sightseeing')} error={errors.sightseeing} />
        </div>
        <p className="mt-3 text-xs text-on-surface-variant">
          {t('Applied as the base price of trips generated from now on. Trips that already exist keep their price.')}
        </p>
      </Panel>
      <Panel title="Seat multiplier per cabin">
        <div className="grid grid-cols-3 gap-3">
          <TextField label="Front cabin" inputMode="decimal" value={form.front} onChange={set('front')} error={errors.front} />
          <TextField label="Standard" inputMode="decimal" value={form.standard} onChange={set('standard')} error={errors.standard} />
          <TextField label="Outdoor deck" inputMode="decimal" value={form.outdoor} onChange={set('outdoor')} error={errors.outdoor} />
        </div>
        <table className="mt-4 w-full text-sm">
          <thead className="text-xs uppercase text-on-surface-variant">
            <tr>
              <th className="py-1 text-left font-semibold">{t('Seat price (VND)')}</th>
              <th className="py-1 text-right font-semibold">{t('Front cabin')}</th>
              <th className="py-1 text-right font-semibold">{t('Standard')}</th>
              <th className="py-1 text-right font-semibold">{t('Outdoor deck')}</th>
            </tr>
          </thead>
          <tbody>
            {[
              { label: 'Regular', base: form.commuter },
              { label: 'Sightseeing', base: form.sightseeing },
            ].map((row) => (
              <tr key={row.label} className="border-t border-outline-variant/30">
                <td className="py-1.5">{t(row.label)}</td>
                <td className="py-1.5 text-right">{preview(row.base, form.front)}</td>
                <td className="py-1.5 text-right">{preview(row.base, form.standard)}</td>
                <td className="py-1.5 text-right">{preview(row.base, form.outdoor)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Panel>
      <div className="flex flex-wrap items-center gap-3 xl:col-span-2">
        <ActionButton type="submit" disabled={busy} icon="save">
          {busy ? t('Saving…') : t('Save fares')}
        </ActionButton>
        {saved && <Notice tone="success">{t('Fares saved.')}</Notice>}
        {error && <Notice tone="error">{error}</Notice>}
      </div>
    </form>
  )
}

export default function FaresPage() {
  useDocumentTitle('Fares')
  const fares = useFetch(getFares)

  if (fares.error) return <ErrorState onRetry={fares.retry} />
  if (fares.loading || !fares.data) return <PageLoader />

  return (
    <>
      <PageHeader
        title="Fares"
        description="Base price of a trip and the seat multiplier of each cabin."
      />
      <FaresForm fares={fares.data} />
    </>
  )
}
