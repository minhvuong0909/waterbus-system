import { useEffect, useId } from 'react'
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  SelectHTMLAttributes,
} from 'react'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import type { AdminTripStatus } from '../types'

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="font-headline-md text-2xl font-bold tracking-tight text-deep-river">
          {t(title)}
        </h1>
        {description && <p className="mt-1 text-sm text-on-surface-variant">{t(description)}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

export function Panel({
  title,
  children,
  className = '',
}: {
  title?: string
  children: ReactNode
  className?: string
}) {
  return (
    <section
      className={`rounded-xl border border-outline-variant/40 bg-white p-4 shadow-xs ${className}`.trim()}
    >
      {title && <h2 className="mb-3 text-sm font-bold uppercase tracking-wide text-deep-river">{t(title)}</h2>}
      {children}
    </section>
  )
}

export interface Column<T> {
  header: string
  render: (row: T) => ReactNode
  className?: string
}

/** Dense table for the admin lists. */
export function DataTable<T>({
  columns,
  rows,
  rowKey,
  empty = 'Nothing here yet.',
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  empty?: string
}) {
  return (
    <div className="overflow-x-auto rounded-xl border border-outline-variant/40 bg-white">
      <table className="w-full min-w-[40rem] text-left text-sm">
        <thead className="border-b border-outline-variant/40 bg-surface-container-low text-xs uppercase tracking-wide text-on-surface-variant">
          <tr>
            {columns.map((c) => (
              <th key={c.header} className={`px-4 py-2.5 font-semibold ${c.className ?? ''}`.trim()}>
                {t(c.header)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-outline-variant/30">
          {rows.length === 0 ? (
            <tr>
              <td colSpan={columns.length} className="px-4 py-8 text-center text-on-surface-variant">
                {t(empty)}
              </td>
            </tr>
          ) : (
            rows.map((row) => (
              <tr key={rowKey(row)} className="hover:bg-mist/60">
                {columns.map((c) => (
                  <td key={c.header} className={`px-4 py-2.5 text-deep-river ${c.className ?? ''}`.trim()}>
                    {c.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}

/** Dialog with a title, closed by Escape, the backdrop or the close button. */
export function Modal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  const titleId = useId()
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label={t('Close')}
        onClick={onClose}
        className="absolute inset-0 cursor-default bg-deep-river/40"
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="relative max-h-full w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-5 shadow-xl"
      >
        <div className="mb-4 flex items-center justify-between gap-3">
          <h2 id={titleId} className="font-headline-sm text-lg font-bold text-deep-river">
            {t(title)}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('Close')}
            className="flex h-8 w-8 items-center justify-center rounded-full hover:bg-surface-container"
          >
            <Icon name="close" className="text-[18px]" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}

const inputClass =
  'w-full rounded-lg border border-outline-variant bg-white px-3 py-2 text-sm text-deep-river focus:border-teal-flow focus:ring-teal-flow disabled:bg-surface-container-low'

interface FieldShellProps {
  label: string
  hint?: string
  error?: string
  children: (id: string) => ReactNode
}

function FieldShell({ label, hint, error, children }: FieldShellProps) {
  const id = useId()
  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-xs font-semibold text-on-surface-variant">
        {t(label)}
      </label>
      {children(id)}
      {hint && !error && <p className="mt-1 text-[11px] text-on-surface-variant">{t(hint)}</p>}
      {error && (
        <p role="alert" className="mt-1 text-[11px] text-coral-glow">
          {error}
        </p>
      )}
    </div>
  )
}

export function TextField({
  label,
  hint,
  error,
  ...input
}: { label: string; hint?: string; error?: string } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(id) => <input id={id} className={inputClass} aria-invalid={Boolean(error)} {...input} />}
    </FieldShell>
  )
}

export function SelectField({
  label,
  hint,
  error,
  children,
  ...select
}: { label: string; hint?: string; error?: string } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <FieldShell label={label} hint={hint} error={error}>
      {(id) => (
        <select id={id} className={inputClass} aria-invalid={Boolean(error)} {...select}>
          {children}
        </select>
      )}
    </FieldShell>
  )
}

type ButtonTone = 'primary' | 'secondary' | 'danger'
const TONES: Record<ButtonTone, string> = {
  primary: 'bg-teal-flow text-on-primary hover:bg-secondary',
  secondary: 'border border-outline-variant bg-white text-deep-river hover:bg-surface-container-low',
  danger: 'bg-coral-glow text-white hover:opacity-90',
}

export function ActionButton({
  tone = 'primary',
  icon,
  children,
  className = '',
  ...rest
}: {
  tone?: ButtonTone
  icon?: string
  children: ReactNode
  className?: string
} & ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      className={`inline-flex items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${TONES[tone]} ${className}`.trim()}
      {...rest}
    >
      {icon && <Icon name={icon} className="text-[18px]" />}
      {children}
    </button>
  )
}

const STATUS_STYLE: Record<AdminTripStatus, string> = {
  Scheduled: 'bg-surface-container text-deep-river',
  Boarding: 'bg-signal-amber/20 text-deep-river',
  InTransit: 'bg-teal-flow/15 text-teal-flow',
  Completed: 'bg-sand-light text-on-surface-variant',
  Cancelled: 'bg-coral-glow/15 text-coral-glow',
  Delayed: 'bg-signal-amber/30 text-deep-river',
}

const STATUS_LABEL: Record<AdminTripStatus, string> = {
  Scheduled: 'Scheduled',
  Boarding: 'Boarding',
  InTransit: 'In transit',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Delayed: 'Delayed',
}

export function StatusBadge({ status }: { status: AdminTripStatus }) {
  return (
    <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_STYLE[status]}`}>
      {t(STATUS_LABEL[status])}
    </span>
  )
}

/** Inline result of an action: green for success, red for an error. */
export function Notice({ tone, children }: { tone: 'success' | 'error'; children: ReactNode }) {
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
