import { Icon } from '../../../components/ui'
import { usePopoverList } from '../../../hooks/usePopoverList'
import type { PierOption } from '../../trips/types'

interface PierFieldProps {
  label: string
  dotClass: string
  value: PierOption
  options: PierOption[]
  onChange: (id: string) => void
  className?: string
  /** Gọn một dòng cho thanh tìm chuyến: bỏ địa chỉ phụ, thấp hơn. */
  compact?: boolean
}

/** Search-form card that shows a pier and opens a small list to pick another one. */
export default function PierField({
  label,
  dotClass,
  value,
  options,
  onChange,
  className = '',
  compact = false,
}: PierFieldProps) {
  const { root, open, setOpen, toggle, placement } = usePopoverList(options.length * 58 + 8)

  return (
    <div ref={root} className={`relative ${className}`.trim()}>
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={toggle}
        className={`group flex h-full w-full min-w-0 flex-col justify-between rounded-xl border border-outline-variant/50 bg-mist text-left transition-colors hover:border-teal-flow/40 hover:bg-sand-light/80 ${
          compact ? 'min-h-[64px] gap-1 px-3.5 py-2.5' : 'min-h-[112px] gap-3 p-3.5'
        }`}
      >
        <span className="mb-1 flex w-full items-center justify-between">
          <span className="flex items-center gap-1.5 text-2xs font-bold uppercase tracking-wider text-on-surface-variant">
            <span className={`h-2 w-2 rounded-full ${dotClass}`} />
            {label}
          </span>
          <Icon
            name="expand_more"
            className="text-base text-outline transition-colors group-hover:text-deep-river"
          />
        </span>
        <span className="block w-full min-w-0">
          <span className="block truncate font-display text-base font-bold text-deep-river">
            {value.name}
          </span>
          {!compact && (
            <span className="mt-0.5 block truncate text-2xs text-on-surface-variant">
              {value.subtitle}
            </span>
          )}
        </span>
      </button>

      {open && (
        <ul
          role="listbox"
          style={{ maxHeight: placement.maxHeight }}
          className={`absolute left-0 right-0 z-30 overflow-y-auto rounded-xl border border-outline-variant/50 bg-white py-1 shadow-lg ${
            placement.up ? 'bottom-full mb-2' : 'top-full mt-2'
          }`}
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
                <span className="text-2xs text-on-surface-variant">{option.subtitle}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
