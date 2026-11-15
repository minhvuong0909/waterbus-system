import { Icon } from '../../../components/ui'

const TONES = {
  teal: 'bg-teal-flow text-on-primary hover:bg-secondary',
  dark: 'bg-deep-river text-on-primary hover:bg-primary-container',
  red: 'bg-coral-glow text-white hover:opacity-90',
}

/** Nút lớn dễ chạm khi đang ở trên tàu. `armed`: đã bấm một lần, bấm lần nữa để xác nhận. */
export default function BigButton({
  icon,
  label,
  tone,
  armed = false,
  disabled,
  onClick,
}: {
  icon: string
  /** Nhãn đã dịch. */
  label: string
  tone: keyof typeof TONES
  armed?: boolean
  disabled?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`flex min-h-[5.5rem] flex-1 flex-col items-center justify-center gap-1 rounded-2xl px-4 py-3 text-lg font-bold shadow-sm transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${TONES[tone]} ${
        armed ? 'ring-4 ring-signal-amber' : ''
      }`}
    >
      <Icon name={icon} className="text-3xl" />
      {label}
    </button>
  )
}
