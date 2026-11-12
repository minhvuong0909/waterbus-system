import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'

const ACTION_CLASS =
  'group mt-space-md flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow py-3 font-display text-base font-semibold text-on-primary shadow-glow transition-colors hover:bg-secondary'

/** Look of the quiet links under the main action ("Change seats", "Back to …"). */
export const SUMMARY_LINK_CLASS = 'text-sm font-semibold text-teal-flow hover:underline'

/**
 * The one main action of a booking step, at the foot of the order summary. A link when `to` is
 * given, otherwise a button. `note` explains what is missing or what happens next; `error` is the
 * reason the action failed.
 */
export default function SummaryAction({
  label,
  to,
  onClick,
  disabled = false,
  note,
  error,
}: {
  /** Already translated. */
  label: string
  to?: string
  onClick?: () => void
  disabled?: boolean
  note?: string
  error?: ReactNode
}) {
  const content = (
    <>
      {label}
      <Icon name="arrow_forward" className="text-lg transition-transform group-hover:translate-x-1 group-disabled:translate-x-0" />
    </>
  )
  return (
    <>
      {to && !disabled ? (
        <Link to={to} className={ACTION_CLASS}>
          {content}
        </Link>
      ) : (
        <button
          type="button"
          onClick={onClick}
          disabled={disabled}
          className={`${ACTION_CLASS} disabled:cursor-not-allowed disabled:opacity-50 disabled:shadow-none`}
        >
          {content}
        </button>
      )}
      {note && (
        <p role="status" className="mt-2 text-center text-xs text-on-surface-variant">
          {note}
        </p>
      )}
      {error && (
        <p role="alert" className="mt-2 text-center text-xs text-coral-glow">
          {error}
        </p>
      )}
    </>
  )
}
