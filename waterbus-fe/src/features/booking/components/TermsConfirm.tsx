import { t } from '../../../i18n'

export default function TermsConfirm({
  checked,
  onChange,
}: {
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 rounded-2xl bg-surface-container px-space-lg py-space-md text-xs text-on-surface-variant">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="h-5 w-5 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow"
      />
      <span>
        {t('I confirm that the booking details above are correct. I accept the')}{' '}
        <span className="text-teal-flow underline">{t('Waterway Carriage Rules')}</span>.
      </span>
    </label>
  )
}
