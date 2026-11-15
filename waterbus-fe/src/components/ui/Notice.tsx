import type { ReactNode } from 'react'

type Tone = 'success' | 'error' | 'warning' | 'info'

const TONE_CLASS: Record<Tone, string> = {
  success: 'bg-teal-flow/10 text-teal-flow',
  error: 'bg-coral-glow/10 text-deep-river',
  warning: 'bg-signal-amber/15 text-deep-river',
  info: 'bg-mist text-on-surface-variant',
}

/**
 * Một dòng thông báo trong trang: kết quả thao tác (`success`/`error`, được đọc cho trình đọc màn hình),
 * lưu ý cần để ý (`warning`) hoặc ghi chú giải thích (`info`).
 */
export default function Notice({ tone, children }: { tone: Tone; children: ReactNode }) {
  return (
    <p
      role={tone === 'error' ? 'alert' : tone === 'success' ? 'status' : undefined}
      className={`rounded-xl px-3 py-2 text-sm ${TONE_CLASS[tone]}`}
    >
      {children}
    </p>
  )
}
