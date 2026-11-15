import { useState } from 'react'
import { Icon } from '../../../components/ui'
import { numberLocale, t } from '../../../i18n'
import type { DateOption } from '../../trips/types'

interface DateCalendarProps {
  /** Các ngày chọn được (`YYYY-MM-DD`); ngày khác trong tháng bị khoá. */
  dates: DateOption[]
  value: string
  onSelect: (id: string) => void
}

const ZONE = 'Asia/Ho_Chi_Minh'
/** Một tuần tính từ thứ Hai, theo lịch Việt Nam. */
const WEEK_DAYS = [0, 1, 2, 3, 4, 5, 6]

const monthOf = (id: string) => id.slice(0, 7)
const utc = (month: string, day: number) => {
  const [year, m] = month.split('-').map(Number)
  return new Date(Date.UTC(year, m - 1, day, 12))
}

/** Lịch tháng của các ngày đang bán, đổi tháng bằng hai nút mũi tên khi khoảng ngày trải qua nhiều tháng. */
export default function DateCalendar({ dates, value, onSelect }: DateCalendarProps) {
  const months = [...new Set(dates.map((d) => monthOf(d.id)))]
  const [shown, setShown] = useState(Math.max(0, months.indexOf(monthOf(value))))
  const month = months[shown] ?? months[0]
  const locale = numberLocale()
  const available = new Set(dates.map((d) => d.id))
  const today = dates.find((d) => d.isToday)?.id

  // Số ô trống đầu tháng để ngày 1 rơi đúng thứ (thứ Hai = 0).
  const lead = (utc(month, 1).getUTCDay() + 6) % 7
  const days = new Date(Date.UTC(Number(month.slice(0, 4)), Number(month.slice(5)), 0)).getUTCDate()

  return (
    <div className="p-3">
      <div className="mb-2 flex items-center justify-between">
        <button
          type="button"
          aria-label={t('Previous month')}
          disabled={shown === 0}
          onClick={() => setShown(shown - 1)}
          className="flex h-8 w-8 items-center justify-center rounded-lg text-deep-river hover:bg-mist disabled:opacity-30"
        >
          <Icon name="chevron_left" className="text-xl" />
        </button>
        <span className="font-display text-sm font-bold capitalize text-deep-river">
          {utc(month, 1).toLocaleDateString(locale, { month: 'long', year: 'numeric', timeZone: ZONE })}
        </span>
        <button
          type="button"
          aria-label={t('Next month')}
          disabled={shown >= months.length - 1}
          onClick={() => setShown(shown + 1)}
          className="flex h-8 w-8 items-center justify-center rounded-lg text-deep-river hover:bg-mist disabled:opacity-30"
        >
          <Icon name="chevron_right" className="text-xl" />
        </button>
      </div>
      <div role="grid" className="grid grid-cols-7 gap-1 text-center">
        {WEEK_DAYS.map((i) => (
          <span key={i} className="py-1 text-3xs font-semibold uppercase text-outline">
            {new Date(Date.UTC(2024, 0, 1 + i, 12)).toLocaleDateString(locale, { weekday: 'short', timeZone: ZONE })}
          </span>
        ))}
        {Array.from({ length: lead }, (_, i) => (
          <span key={`lead-${i}`} />
        ))}
        {Array.from({ length: days }, (_, i) => {
          const day = i + 1
          const id = `${month}-${String(day).padStart(2, '0')}`
          const enabled = available.has(id)
          const selected = id === value
          return (
            <button
              key={id}
              type="button"
              disabled={!enabled}
              aria-pressed={selected}
              aria-current={id === today ? 'date' : undefined}
              onClick={() => onSelect(id)}
              className={`flex h-9 items-center justify-center rounded-lg text-sm font-semibold transition-colors ${
                selected
                  ? 'bg-teal-flow text-on-primary'
                  : enabled
                    ? `text-deep-river hover:bg-mist ${id === today ? 'ring-1 ring-teal-flow/60' : ''}`
                    : 'cursor-not-allowed text-outline/40'
              }`}
            >
              {day}
            </button>
          )
        })}
      </div>
    </div>
  )
}
