import type { StatusLook, StatusTone } from '../utils'

const TONE_CLASS: Record<StatusTone, string> = {
  positive: 'bg-secondary-container text-on-secondary-container',
  pending: 'bg-signal-amber/20 text-deep-river',
  negative: 'bg-coral-glow/15 text-coral-glow',
  neutral: 'bg-surface-container text-on-surface-variant',
}

/** Status of an order or booking as a coloured pill; `look.label` is already translated. */
export default function StatusPill({ look }: { look: StatusLook }) {
  return (
    <span
      className={`inline-flex shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${TONE_CLASS[look.tone]}`}
    >
      {look.label}
    </span>
  )
}
