import { useState } from 'react'
import FormField from './FormField'
import { FORM_INPUT } from '../formStyles'

/** Text input that reports a problem only after the traveler has left the field once. */
export default function FormTextField({
  label,
  hint,
  icon,
  value,
  error,
  onChange,
  type = 'text',
  autoComplete,
  required,
  maxLength,
}: {
  label: string
  hint?: string
  icon?: string
  value: string
  error?: string
  onChange: (value: string) => void
  type?: string
  autoComplete?: string
  required?: boolean
  maxLength?: number
}) {
  const [touched, setTouched] = useState(false)
  return (
    <FormField label={label} hint={hint} icon={icon} required={required} error={touched ? error : undefined}>
      <input
        className={`${FORM_INPUT} ${icon ? 'pl-10' : 'px-3'}`}
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onBlur={() => setTouched(true)}
        autoComplete={autoComplete}
        required={required}
        maxLength={maxLength}
        aria-invalid={touched && error ? true : undefined}
      />
    </FormField>
  )
}
