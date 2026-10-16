import type { HTMLAttributes } from 'react'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** `functional` = trips/tickets/forms, `panel` = large feature/hero panel. */
  tone?: 'functional' | 'panel'
}

export default function Card({ tone = 'functional', className = '', ...rest }: CardProps) {
  const radius = tone === 'panel' ? 'rounded-panel' : 'rounded-card'
  return (
    <div
      className={`${radius} border border-outline-variant/30 bg-surface-container-lowest shadow-[0_2px_12px_rgba(13,37,56,0.04)] ${className}`.trim()}
      {...rest}
    />
  )
}
