import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../../components/ui'
import type { PierOption } from '../types'

/** Chiều cao thanh header cố định phía trên trang (h-20 trên màn hình rộng). */
const HEADER_HEIGHT = 80

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
  const [open, setOpen] = useState(false)
  // Danh sách mở phía dưới nếu đủ chỗ, không thì phía trên nếu đủ chỗ. Nếu cả hai đều chật, mở
  // phía dưới và cuộn nhẹ trang để hiện đủ danh sách (không để cắt mất bến cuối, không chui dưới
  // thanh header cố định). Màn hình quá thấp thì danh sách tự cuộn bên trong.
  const [placement, setPlacement] = useState<{ up: boolean; maxHeight: number }>({
    up: false,
    maxHeight: 360,
  })
  const root = useRef<HTMLDivElement>(null)

  const toggle = () => {
    if (!open && root.current) {
      const rect = root.current.getBoundingClientRect()
      const margin = 16
      const below = window.innerHeight - rect.bottom - margin
      const above = rect.top - HEADER_HEIGHT - margin
      const listHeight = options.length * 58 + 8
      const maxHeight = Math.min(listHeight, window.innerHeight - HEADER_HEIGHT - 2 * margin)
      if (below >= maxHeight) setPlacement({ up: false, maxHeight })
      else if (above >= maxHeight) setPlacement({ up: true, maxHeight })
      else {
        setPlacement({ up: false, maxHeight })
        window.scrollBy({ top: maxHeight - below, behavior: 'smooth' })
      }
    }
    setOpen((v) => !v)
  }

  // Bấm ra ngoài hoặc nhấn Esc thì đóng danh sách.
  useEffect(() => {
    if (!open) return
    const onPointerDown = (event: PointerEvent) => {
      if (root.current && !root.current.contains(event.target as Node)) setOpen(false)
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false)
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  return (
    <div ref={root} className={`relative ${className}`.trim()}>
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={toggle}
        className={`group flex h-full w-full min-w-0 flex-col justify-between rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] text-left transition-colors hover:border-teal-flow/40 hover:bg-[#EBF2F0]/80 ${
          compact ? 'min-h-[64px] gap-1 px-3.5 py-2.5' : 'min-h-[112px] gap-3 p-3.5'
        }`}
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
        <span className="block w-full min-w-0">
          <span className="block truncate font-headline-sm text-base font-bold text-deep-river">
            {value.name}
          </span>
          {!compact && (
            <span className="mt-0.5 block truncate text-[11px] text-on-surface-variant">
              {value.subtitle}
            </span>
          )}
        </span>
      </button>

      {open && (
        <ul
          role="listbox"
          style={{ maxHeight: placement.maxHeight }}
          className={`absolute left-0 right-0 z-30 overflow-y-auto rounded-xl border border-[#E2ECEE] bg-white py-1 shadow-lg ${
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
                <span className="text-[11px] text-on-surface-variant">{option.subtitle}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
