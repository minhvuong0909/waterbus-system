import { useId, useRef } from 'react'
import type { KeyboardEvent, ReactNode } from 'react'
import { t } from '../../i18n'

export interface TabItem<K extends string> {
  key: K
  /** Chuỗi nguồn tiếng Anh. */
  label: string
  /** Số hiện cạnh nhãn (vd số việc đang chờ); ẩn khi không có hoặc bằng 0. */
  count?: number
}

/**
 * Thanh tab theo mẫu WAI-ARIA: mũi tên trái/phải, Home/End để chuyển tab. Nội dung tab là `children`
 * (chỉ render tab đang chọn), gắn với tab bằng `aria-labelledby`.
 */
export default function Tabs<K extends string>({
  label,
  tabs,
  value,
  onChange,
  children,
}: {
  label: string
  tabs: TabItem<K>[]
  value: K
  onChange: (key: K) => void
  children: ReactNode
}) {
  const id = useId()
  const buttons = useRef<(HTMLButtonElement | null)[]>([])
  const index = Math.max(0, tabs.findIndex((tab) => tab.key === value))

  const move = (event: KeyboardEvent) => {
    const last = tabs.length - 1
    const next =
      event.key === 'ArrowRight' ? (index === last ? 0 : index + 1)
        : event.key === 'ArrowLeft' ? (index === 0 ? last : index - 1)
          : event.key === 'Home' ? 0
            : event.key === 'End' ? last
              : null
    if (next === null) return
    event.preventDefault()
    onChange(tabs[next].key)
    buttons.current[next]?.focus()
  }

  return (
    <div>
      <div
        role="tablist"
        aria-label={t(label)}
        onKeyDown={move}
        className="mb-4 flex gap-1 overflow-x-auto border-b border-outline-variant/30 [scrollbar-width:none]"
      >
        {tabs.map((tab, i) => {
          const selected = tab.key === value
          return (
            <button
              key={tab.key}
              ref={(el) => {
                buttons.current[i] = el
              }}
              type="button"
              role="tab"
              id={`${id}-tab-${tab.key}`}
              aria-selected={selected}
              aria-controls={`${id}-panel`}
              tabIndex={selected ? 0 : -1}
              onClick={() => onChange(tab.key)}
              className={`-mb-px inline-flex min-h-[44px] shrink-0 items-center gap-2 whitespace-nowrap border-b-2 px-3 text-sm font-semibold transition-colors ${
                selected
                  ? 'border-teal-flow text-teal-flow'
                  : 'border-transparent text-on-surface-variant hover:text-deep-river'
              }`}
            >
              {t(tab.label)}
              {tab.count ? (
                <span
                  className={`rounded-full px-2 py-0.5 text-2xs font-bold tabular-nums ${
                    selected ? 'bg-teal-flow text-white' : 'bg-surface-container text-deep-river'
                  }`}
                >
                  {tab.count}
                </span>
              ) : null}
            </button>
          )
        })}
      </div>
      <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-tab-${tabs[index]?.key}`}>
        {children}
      </div>
    </div>
  )
}
