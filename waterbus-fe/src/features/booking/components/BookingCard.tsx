import type { ReactNode } from 'react'

/** White rounded card shell used by the booking pages. */
export default function BookingCard({
  className = '',
  children,
}: {
  className?: string
  children: ReactNode
}) {
  return (
    <section
      className={`rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)] ${className}`.trim()}
    >
      {children}
    </section>
  )
}
