import { useState } from 'react'
import { Icon } from '../../../components/ui'
import type { PierOption } from '../types'

interface PierFieldProps {
  label: string
  dotClass: string
  value: PierOption
  options: PierOption[]
  onChange: (id: string) => void
  className?: string
}

/** Search-form card that shows a pier and opens a small list to pick another one. */
export default function PierField({
  label,
  dotClass,
  value,
  options,
  onChange,
  className = '',
}: PierFieldProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className={`relative ${className}`.trim()}>
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
        className="group flex h-full w-full flex-col justify-between rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] p-3.5 text-left transition-colors hover:bg-[#EBF2F0]/80"
      >
        <span className="mb-1 flex w-full items-center justify-between">
          <span className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-on-surface-variant">
            <span className={`h-2 w-2 rounded-full ${dotClass}`} />
            {label}
          </span>
          <Icon
            name="expand_more"
            className="text-[16px] text-outline transition-colors group-hover:text-deep-river"
          />
        </span>
        <span>
          <span className="block truncate font-headline-sm text-base font-bold text-deep-river">
            {value.name}
          </span>
          <span className="mt-0.5 block truncate text-[11px] text-on-surface-variant">
            {value.subtitle}
          </span>
        </span>
      </button>

      {open && (
        <ul
          role="listbox"
          className="absolute left-0 right-0 top-full z-30 mt-2 overflow-hidden rounded-xl border border-[#E2ECEE] bg-white py-1 shadow-lg"
        >
          {options.map((option) => (
            <li key={option.id}>
              <button
                type="button"
                role="option"
                aria-selected={option.id === value.id}
                onClick={() => {
                  onChange(option.id)
                  setOpen(false)
                }}
                className={`flex w-full flex-col px-3.5 py-2 text-left hover:bg-mist ${
                  option.id === value.id ? 'bg-sand-light/70' : ''
                }`}
              >
                <span className="text-sm font-semibold text-deep-river">{option.name}</span>
                <span className="text-[11px] text-on-surface-variant">{option.subtitle}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
