import { Icon } from '../../../components/ui'
import { REQUIRED_LABEL_CLASS } from '../../../components/ui/styles'

export default function FormField({
  label,
  hint,
  icon,
  error,
  required,
  children,
}: {
  label: string
  hint?: string
  icon?: string
  /** Message shown under the field; empty when the value is fine or not checked yet. */
  error?: string
  /** Ô bắt buộc: nhãn có dấu (*) đỏ. */
  required?: boolean
  children: React.ReactNode
}) {
  return (
    <label className="block">
      <span className="mb-1.5 flex items-center justify-between text-xs font-semibold text-on-surface">
        <span className={required ? REQUIRED_LABEL_CLASS : undefined}>{label}</span>
        {hint && <span className="font-normal text-on-surface-variant">{hint}</span>}
      </span>
      <span className="relative block">
        {icon && (
          <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-outline">
            <Icon name={icon} className="text-lg" />
          </span>
        )}
        {children}
      </span>
      {error && (
        <span role="alert" className="mt-1 block text-xs text-coral-glow">
          {error}
        </span>
      )}
    </label>
  )
}
