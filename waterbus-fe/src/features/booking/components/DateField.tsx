import { Icon } from '../../../components/ui'
import { usePopoverList } from '../../../hooks/usePopoverList'
import type { DateOption } from '../../trips/types'
import { formatDateLabel } from '../../trips/utils'
import { t } from '../../../i18n'

interface DateFieldProps {
  dates: DateOption[]
  value: string
  onChange: (id: string) => void
  /** Gọn một dòng cho thanh tìm chuyến. */
  compact?: boolean
  /** Nhãn của ô, mặc định "Ngày"; chuyến khứ hồi dùng "Ngày đi" và "Ngày về". */
  label?: string
}

/** Ngày trong tháng (1-31) lấy từ id `YYYY-MM-DD`. */
const dayOfMonth = (id: string) => Number(id.slice(8, 10))

/** Ô chọn ngày: bấm mở danh sách các ngày đang bán, mỗi ngày là một dòng có ô thứ/ngày và nhãn Hôm nay/Ngày mai. */
export default function DateField({ dates, value, onChange, compact = false, label }: DateFieldProps) {
  const { root, open, setOpen, toggle, placement } = usePopoverList(dates.length * 60 + 8)
  const selected = dates.find((d) => d.id === value) ?? dates[0]
  const firstIsToday = Boolean(dates[0]?.isToday)

  // Nhãn "Hôm nay" đã nằm trong tên ngày nên chỉ thêm nhãn cho ngày mai.
  const tagOf = (index: number): string | null => (index === 1 && firstIsToday ? t('Tomorrow') : null)

  return (
    <div ref={root} className="relative h-full min-w-0">
      <button
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        onClick={toggle}
        className={`group flex h-full w-full min-w-0 flex-col justify-between rounded-xl border border-outline-variant/50 bg-mist text-left transition-colors hover:border-teal-flow/40 hover:bg-sand-light/80 ${
          compact ? 'min-h-[64px] gap-1 px-3.5 py-2.5' : 'min-h-[112px] gap-3 p-3.5'
        }`}
      >
        <span className="flex w-full items-center justify-between">
          <span className="flex items-center gap-1.5 text-2xs font-bold uppercase tracking-wider text-on-surface-variant">
            <Icon name="calendar_today" className="text-sm text-teal-flow" />
            {label ?? t('Date')}
          </span>
          <Icon
            name="expand_more"
            className={`text-base text-outline transition-transform group-hover:text-deep-river ${
              open ? 'rotate-180' : ''
            }`}
          />
        </span>
        <span className="block truncate font-display text-base font-bold text-deep-river">
          {formatDateLabel(selected)}
        </span>
      </button>

      {open && (
        <ul
          role="listbox"
          aria-label={label ?? t('Departure Date')}
          style={{ maxHeight: placement.maxHeight }}
          className={`absolute left-0 right-0 z-30 min-w-[16rem] overflow-y-auto rounded-xl border border-outline-variant/50 bg-white py-1 shadow-lg ${
            placement.up ? 'bottom-full mb-2' : 'top-full mt-2'
          }`}
        >
          {dates.map((date, i) => {
            const isSelected = date.id === selected.id
            const tag = tagOf(i)
            return (
              <li key={date.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => {
                    onChange(date.id)
                    setOpen(false)
                  }}
                  className={`flex w-full items-center gap-3 px-3 py-2 text-left transition-colors hover:bg-mist ${
                    isSelected ? 'bg-sand-light/70' : ''
                  }`}
                >
                  <span
                    className={`flex h-10 w-10 shrink-0 flex-col items-center justify-center rounded-lg leading-none ${
                      isSelected ? 'bg-teal-flow text-on-primary' : 'bg-mist text-deep-river'
                    }`}
                  >
                    <span className="text-3xs font-semibold uppercase tracking-wide opacity-80">
                      {date.weekday}
                    </span>
                    <span className="mt-0.5 font-display text-sm font-bold">{dayOfMonth(date.id)}</span>
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-sm font-semibold text-deep-river">
                      {formatDateLabel(date)}
                    </span>
                    {tag && <span className="text-2xs font-medium text-teal-flow">{tag}</span>}
                  </span>
                  {isSelected && <Icon name="check" className="text-lg text-teal-flow" />}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
