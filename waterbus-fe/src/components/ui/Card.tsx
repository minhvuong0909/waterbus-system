import type { HTMLAttributes } from 'react'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  /** `functional` = trips/tickets/forms, `panel` = large feature/hero panel. */
  tone?: 'functional' | 'panel'
}

export default function Card({ tone = 'functional', className = '', ...rest }: CardProps) {
  const radius = tone === 'panel' ? 'rounded-panel' : 'rounded-card'
  return (
    <div
      className={`${radius} border border-outline-variant/30 bg-surface-container-lowest shadow-card ${className}`.trim()}
      {...rest}
    />
  )
}
