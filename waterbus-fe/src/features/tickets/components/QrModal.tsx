import { useEffect } from 'react'
import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

/** Full-screen QR view for fast turnstile scans. Closes on Escape or backdrop click. */
export default function QrModal({ ticket, onClose }: { ticket: Ticket; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={t('Boarding QR Code')}
      className="fixed inset-0 z-[60] flex items-center justify-center bg-deep-river/70 p-space-md backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="relative w-full max-w-sm rounded-3xl bg-white p-space-xl text-center shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label={t('Close')}
          className="absolute right-4 top-4 flex h-9 w-9 items-center justify-center rounded-full bg-surface-container text-deep-river hover:bg-surface-container-high"
        >
          <Icon name="close" className="text-[20px]" />
        </button>
        <span className="text-[11px] font-bold uppercase tracking-wider text-teal-flow">
          {t('Fast Turnstile Scan')}
        </span>
        <h2 className="mt-1 font-headline-sm text-2xl font-bold text-deep-river">
          {t('Boarding QR Code')}
        </h2>
        <p className="text-xs text-on-surface-variant">
          {ticket.departPier} → {ticket.arrivePier} ({ticket.seat})
        </p>
        <div className="mx-auto mt-space-md w-fit rounded-2xl border border-outline-variant/40 p-3">
          {ticket.qrPayload && <QrCode value={ticket.qrPayload} size={260} />}
        </div>
        <div className="mt-space-md font-mono text-lg font-bold tracking-wider text-deep-river">
          {ticket.ticketCode}
        </div>
        <div className="text-xs text-on-surface-variant">
          {t('Single Crossing Pass • {price}', { price: formatVndSuffix(ticket.fareVnd) })}
        </div>
        <button
          type="button"
          onClick={onClose}
          className="mt-space-lg w-full rounded-xl bg-teal-flow py-3 text-sm font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('Done')}
        </button>
      </div>
    </div>
  )
}
