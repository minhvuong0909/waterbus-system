import type { ReactNode } from 'react'

/** Inline result of an action: green for success, red for an error. */
export default function Notice({ tone, children }: { tone: 'success' | 'error'; children: ReactNode }) {
  return (
    <p
      role={tone === 'error' ? 'alert' : 'status'}
      className={`rounded-lg px-3 py-2 text-sm ${
        tone === 'error' ? 'bg-coral-glow/10 text-deep-river' : 'bg-teal-flow/10 text-teal-flow'
      }`}
    >
      {children}
    </p>
  )
}
