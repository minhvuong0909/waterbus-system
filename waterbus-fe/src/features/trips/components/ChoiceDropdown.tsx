import { Icon } from '../../../components/ui'
import { usePopoverList } from '../hooks/usePopoverList'

export interface Choice {
  id: string
  label: string
}

interface ChoiceDropdownProps {
  /** Nhãn nhỏ phía trước giá trị, vd "Loại chuyến:". */
  label: string
  icon?: string
  options: Choice[]
  value: string
  onChange: (id: string) => void
  className?: string
}

/** Ô chọn một trong vài lựa chọn (bộ lọc, sắp xếp): cùng kiểu với ô bến và ô ngày, thay cho <select> mặc định. */
export default function ChoiceDropdown({
  label,
  icon,
  options,
  value,
  onChange,
  className = '',
}: ChoiceDropdownProps) {
  const { root, open, setOpen, toggle, placement } = usePopoverList(options.length * 44 + 8)
  const selected = options.find((o) => o.id === value) ?? options[0]

  return (
    <div ref={root} className={`relative min-w-0 ${className}`.trim()}>
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-label={label}
        onClick={toggle}
        className="group flex min-h-[44px] w-full min-w-0 items-center gap-1.5 rounded-xl border border-deep-river/10 bg-white px-3 text-left shadow-sm transition-colors hover:border-teal-flow/40 focus-visible:border-teal-flow/60"
      >
        {icon && <Icon name={icon} className="text-[15px] text-teal-flow" />}
        <span className="shrink-0 text-xs text-outline">{label}</span>
        <span className="min-w-0 flex-1 truncate text-sm font-semibold text-deep-river">
          {selected?.label}
        </span>
        <Icon
          name="expand_more"
          className={`text-[18px] text-outline transition-transform group-hover:text-deep-river ${
            open ? 'rotate-180' : ''
          }`}
        />
      </button>

      {open && (
        <ul
          role="listbox"
          aria-label={label}
          style={{ maxHeight: placement.maxHeight }}
          className={`absolute left-0 right-0 z-30 min-w-[12rem] overflow-y-auto rounded-xl border border-[#E2ECEE] bg-white py-1 shadow-lg ${
            placement.up ? 'bottom-full mb-2' : 'top-full mt-2'
          }`}
        >
          {options.map((option) => {
            const isSelected = option.id === selected?.id
            return (
              <li key={option.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => {
                    onChange(option.id)
                    setOpen(false)
                  }}
                  className={`flex min-h-[44px] w-full items-center justify-between gap-3 px-3.5 py-2 text-left text-sm transition-colors hover:bg-mist ${
                    isSelected ? 'bg-sand-light/70 font-semibold text-deep-river' : 'text-on-surface'
                  }`}
                >
                  {option.label}
                  {isSelected && <Icon name="check" className="text-[18px] text-teal-flow" />}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
