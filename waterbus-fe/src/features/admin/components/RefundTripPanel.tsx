import { useMemo, useState } from 'react'
import { Button, DataTable, Notice, Pagination, Panel, SearchSelect } from '../../../components/ui'
import type { Column } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { formatVnd } from '../../../utils/format'
import { formatDateTime } from '../../../utils/time'
import { useReload, useSubmit } from '../hooks/useAdmin'
import { getOperationTrips } from '../services/operationsService'
import { listRefundCandidates, requestRefundsForTrip } from '../services/refundService'
import type { TripStatus } from '../../trips/types'
import type { BatchRefundRequestResult, RefundCandidate } from '../types'

/** Chuyến bị huỷ/chấm dứt vẫn cần hoàn tiền sau nhiều ngày: tìm trong 23 ngày qua và 7 ngày tới (BE cho tối đa 31 ngày). */
const LOOKBACK_DAYS = 23
const LOOKAHEAD_DAYS = 7
const DAY_MS = 24 * 60 * 60 * 1000

function refundableTripsQuery() {
  const now = Date.now()
  return {
    from: new Date(now - LOOKBACK_DAYS * DAY_MS).toISOString(),
    to: new Date(now + LOOKAHEAD_DAYS * DAY_MS).toISOString(),
    statuses: ['Cancelled', 'Terminated'] as TripStatus[],
  }
}

/**
 * Hoàn theo chuyến: chọn chuyến đã bị huỷ/chấm dứt, xem Booking cần hoàn (BR-RF-01..09), rồi tạo yêu cầu hoàn
 * hàng loạt. Bước này KHÔNG chuyển tiền: nhân viên hoàn qua cổng merchant theo file CSV, sau đó Admin xác nhận.
 */
export default function RefundTripPanel({ onCreated }: { onCreated: () => void }) {
  const trips = useFetch(() => getOperationTrips(refundableTripsQuery()))
  const [tripId, setTripId] = useState('')
  const [pageNumber, setPageNumber] = useState(1)
  const [version, reload] = useReload()
  const [confirming, setConfirming] = useState(false)
  const [result, setResult] = useState<BatchRefundRequestResult | null>(null)
  const { run, busy, error } = useSubmit()

  const refundable = useMemo(
    () =>
      (trips.data ?? [])
        .filter((trip) => trip.status === 'Cancelled' || trip.status === 'Terminated')
        .sort((a, b) => b.departureTime.localeCompare(a.departureTime)),
    [trips.data],
  )
  const candidates = useFetch(
    () => (tripId ? listRefundCandidates(tripId, pageNumber, 20) : Promise.resolve(undefined)),
    [tripId, pageNumber, version],
  )

  const rows = candidates.data?.items ?? []
  const total = rows.reduce((sum, row) => sum + row.amount, 0)

  const chooseTrip = (id: string) => {
    setTripId(id)
    setPageNumber(1)
    setConfirming(false)
    setResult(null)
  }
  const create = async () => {
    const created = await run(() => requestRefundsForTrip(tripId))
    setConfirming(false)
    if (!created) return
    setResult(created)
    reload()
    onCreated()
  }

  const columns: Column<RefundCandidate>[] = [
    { header: 'Booking', render: (x) => <span className="font-mono font-semibold">{x.bookingCode}</span> },
    {
      header: 'Buyer',
      render: (x) => (
        <>
          <div className="font-semibold">{x.purchaserName}</div>
          <div className="text-xs text-on-surface-variant">{x.purchaserEmail}</div>
        </>
      ),
    },
    { header: 'Tickets', render: (x) => <span className="font-mono text-xs">{x.ticketCodes.join(', ')}</span> },
    { header: 'Reason', render: (x) => x.reason },
    { header: 'Amount', render: (x) => formatVnd(x.amount), className: 'text-right' },
  ]

  return (
    <Panel title="Refund a cancelled or terminated trip">
      <p className="mb-4 text-sm text-on-surface-variant">
        {t('Creating requests does not move money. Staff refund each buyer through the VNPay merchant portal using the CSV below, then you confirm.')}
      </p>
      {trips.error ? (
        <Notice tone="error">{t('Something went wrong. Please check your connection and try again.')}</Notice>
      ) : (
        <SearchSelect label="Trip" value={tripId} onChange={chooseTrip} disabled={!trips.data}
          placeholder="Select a trip…"
          hint={trips.data && refundable.length === 0 ? t('No cancelled or terminated trips.') : t('Search by route, boat or departure time.')}
          options={refundable.map((trip) => ({ value: trip.id, label: trip.routeName,
            description: formatDateTime(trip.departureTime), badge: trip.boatCode ?? t(trip.status),
          }))} />
      )}

      {tripId && candidates.error && (
        <div className="mt-4">
          <Notice tone="error">{t('Could not load the bookings to refund.')}</Notice>
        </div>
      )}
      {tripId && candidates.data && (
        <div className="mt-4 space-y-4">
          <DataTable columns={columns} rows={rows} rowKey={(x) => x.bookingId} empty="No bookings are waiting for a refund." />
          {candidates.data && (
            <Pagination
              pageNumber={candidates.data.pageNumber}
              totalPages={candidates.data.totalPages}
              onChange={setPageNumber}
              busy={candidates.fetching}
            />
          )}
          {rows.length > 0 && (
            <div className="flex flex-wrap items-center gap-3">
              <p className="text-sm font-semibold text-deep-river">
                {candidates.data.totalPages > 1
                  ? t('{count} bookings on this page • {amount}', { count: rows.length, amount: formatVnd(total) })
                  : t('{count} bookings • {amount}', { count: candidates.data.totalCount, amount: formatVnd(total) })}
              </p>
              {candidates.data.totalPages > 1 && (
                <span className="text-xs text-on-surface-variant">
                  {t('{count} bookings in total across all pages', { count: candidates.data.totalCount })}
                </span>
              )}
              {confirming ? (
                <>
                  <Button variant="danger" icon="receipt_long" onClick={create} disabled={busy}>
                    {busy ? t('Sending…') : t('Yes, create the refund requests')}
                  </Button>
                  <Button variant="neutral" onClick={() => setConfirming(false)} disabled={busy}>
                    {t('Cancel')}
                  </Button>
                  <p className="basis-full text-sm text-on-surface-variant">
                    {t('Tickets and bookings are cancelled as soon as the requests are created.')}
                  </p>
                </>
              ) : (
                <Button icon="receipt_long" onClick={() => setConfirming(true)}>
                  {t('Create refund requests')}
                </Button>
              )}
            </div>
          )}
        </div>
      )}
      {error && (
        <div className="mt-4">
          <Notice tone="error">{error}</Notice>
        </div>
      )}
      {result && (
        <div className="mt-4 space-y-2">
          <Notice tone="success">
            {t('Created {created} refund requests, skipped {skipped}.', { created: result.requested, skipped: result.skipped })}
          </Notice>
          {result.skippedItems.length > 0 && (
            <ul className="divide-y divide-outline-variant/30 rounded-xl border border-outline-variant/30 text-sm">
              {result.skippedItems.map((item) => (
                <li key={item.bookingId} className="px-3 py-2">
                  <span className="font-mono font-semibold">{item.bookingCode}</span>
                  <span className="ml-2 text-on-surface-variant">{item.reason}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </Panel>
  )
}
