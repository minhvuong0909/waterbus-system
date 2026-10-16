interface IconProps {
  name: string
  filled?: boolean
  className?: string
}

/** Material Symbols Outlined glyph. Size it with a text-size class, e.g. `text-[20px]`. */
export default function Icon({ name, filled = false, className = '' }: IconProps) {
  return (
    <span
      aria-hidden="true"
      className={`material-symbols-outlined${filled ? ' filled' : ''} ${className}`.trim()}
    >
      {name}
    </span>
  )
}
