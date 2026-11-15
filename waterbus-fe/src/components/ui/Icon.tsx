interface IconProps {
  name: string
  filled?: boolean
  className?: string
}

/** Ký tự Material Symbols Outlined. Đặt cỡ bằng lớp cỡ chữ, vd `text-xl`. */
export default function Icon({ name, filled = false, className = '' }: IconProps) {
  return (
    <span
      aria-hidden="true"
      className={`material-symbols-outlined leading-none${filled ? ' filled' : ''} ${className}`.trim()}
    >
      {name}
    </span>
  )
}
