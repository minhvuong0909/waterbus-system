import { REQUIRED_LABEL_CLASS } from '../../../components/ui/styles'
import { Icon } from '../../../components/ui'
import { usePopoverList } from '../../../hooks/usePopoverList'
import DateCalendar from './DateCalendar'
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

/** Ô chọn ngày: bấm mở lịch tháng của các ngày đang bán. */
export default function DateField({ dates, value, onChange, compact = false, label }: DateFieldProps) {
  const { root, open, setOpen, toggle, placement } = usePopoverList(340)
  const selected = dates.find((d) => d.id === value) ?? dates[0]

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
            <span className={REQUIRED_LABEL_CLASS}>{label ?? t('Date')}</span>
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
        <div
          role="dialog"
          aria-label={label ?? t('Departure Date')}
          className={`absolute left-0 z-30 w-full min-w-[18rem] max-w-[21rem] rounded-xl border border-outline-variant/50 bg-white shadow-lg ${
            placement.up ? 'bottom-full mb-2' : 'top-full mt-2'
          }`}
        >
          <DateCalendar
            dates={dates}
            value={selected.id}
            onSelect={(id) => {
              onChange(id)
              setOpen(false)
            }}
          />
        </div>
      )}
    </div>
  )
}
