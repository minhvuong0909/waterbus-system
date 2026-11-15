import { t } from '../../../i18n'

export type BadgeTone = 'active' | 'upcoming' | 'muted'

const TONE_CLASS: Record<BadgeTone, string> = {
  active: 'bg-teal-flow/15 text-teal-flow',
  upcoming: 'bg-signal-amber/20 text-deep-river',
  muted: 'bg-surface-container text-on-surface-variant',
}

/** Trạng thái một mục danh mục dạng nhãn tròn; `label` là chuỗi nguồn tiếng Anh. */
export default function StatusBadge({ tone, label }: { tone: BadgeTone; label: string }) {
  return (
    <span className={`inline-flex whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-semibold ${TONE_CLASS[tone]}`}>
      {t(label)}
    </span>
  )
}

/** "Đang dùng" hoặc "Đã ngừng" cho các mục bật/tắt được. */
export function ActiveBadge({ active }: { active: boolean }) {
  return <StatusBadge tone={active ? 'active' : 'muted'} label={active ? 'In use' : 'Switched off'} />
}
