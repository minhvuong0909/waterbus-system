import { useState } from 'react'
import type { InputHTMLAttributes, ReactNode } from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

interface AuthFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  /** Material Symbols name shown at the left of the input. */
  icon?: string
  /** Renders a show/hide toggle when the input type is "password". */
  revealable?: boolean
  /** Content on the right of the label row, e.g. "Forgot password?". */
  labelAside?: ReactNode
}

const INPUT_CLASS =
  'w-full rounded-xl border border-outline-variant bg-white py-2.5 text-sm text-ink placeholder:text-outline transition-all focus:border-teal-flow focus:outline-none focus:ring-2 focus:ring-teal-flow/20'

export default function AuthField({
  label,
  icon,
  revealable,
  labelAside,
  id,
  type = 'text',
  ...rest
}: AuthFieldProps) {
  const [shown, setShown] = useState(false)
  const inputType = revealable && shown ? 'text' : type

  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between">
        <label htmlFor={id} className="block text-xs font-semibold text-on-surface">
          {label}
        </label>
        {labelAside}
      </div>
      <div className="relative">
        {icon && (
          <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-outline">
            <Icon name={icon} className="text-[18px]" />
          </div>
        )}
        <input
          id={id}
          type={inputType}
          className={`${INPUT_CLASS} ${icon ? 'pl-10' : 'pl-3.5'} ${revealable ? 'pr-10' : 'pr-4'}`}
          {...rest}
        />
        {revealable && (
          <button
            type="button"
            aria-label={shown ? t('Hide password') : t('Show password')}
            onClick={() => setShown((v) => !v)}
            className="absolute inset-y-0 right-0 flex items-center pr-3.5 text-outline hover:text-on-surface-variant"
          >
            <Icon name={shown ? 'visibility_off' : 'visibility'} className="text-[18px]" />
          </button>
        )}
      </div>
    </div>
  )
}
