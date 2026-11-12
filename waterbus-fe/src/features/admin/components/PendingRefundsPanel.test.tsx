import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import { saveBlob } from '../../../utils/download'
import { confirmRefundsBatch, exportPendingRefundsCsv, listPendingRefunds } from '../services/refundService'
import type { PendingRefund } from '../types'
import PendingRefundsPanel from './PendingRefundsPanel'

vi.mock('../services/refundService')
vi.mock('../../../utils/download', () => ({ saveBlob: vi.fn() }))

const refund = (id: string, code: string, amount: number, extra: Partial<PendingRefund> = {}): PendingRefund => ({
  refundId: id, bookingCode: code, orderId: 'o-' + id, purchaserName: 'Nguyễn An', purchaserEmail: `${code}@example.com`,
  purchaserPhone: null, amount, currency: 'VND', ticketCodes: ['T1'], routeName: 'Bạch Đằng', departureTime: null,
  isLatePayment: false, tripStatus: 'Cancelled', merchantReference: 'TXN-' + code, paymentGatewayReference: null, paidAt: null,
  status: 'Processing', requestedAt: '2026-11-12T01:00:00Z', lastExportedAt: null, ...extra,
})

const page = (items: PendingRefund[], totalCount = items.length) => ({ items, totalCount, pageNumber: 1, totalPages: 1 })

beforeEach(() => {
  vi.mocked(listPendingRefunds).mockResolvedValue(page([refund('r1', 'BK1', 40000), refund('r2', 'BK2', 60000, { isLatePayment: true, tripStatus: null })]))
})

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  setActiveLocale('en')
})

describe('PendingRefundsPanel', () => {
  it('lists what is waiting for staff, with the reason of each', async () => {
    render(<PendingRefundsPanel refreshKey={0} />)

    expect(await screen.findByText('BK1')).toBeTruthy()
    expect(screen.getByText('BK2')).toBeTruthy()
    expect(screen.getByText('Trip cancelled')).toBeTruthy()
    expect(screen.getByText('Late payment (no ticket)')).toBeTruthy()
    expect(screen.getByText('TXN-BK1')).toBeTruthy()
    expect(screen.getByText('2 refunds waiting')).toBeTruthy()
  })

  it('downloads the CSV under the server file name and reloads the list', async () => {
    const blob = new Blob(['csv'])
    vi.mocked(exportPendingRefundsCsv).mockResolvedValue({ blob, fileName: 'hoan-tien-20261112-1005.csv', rowCount: 2 })
    render(<PendingRefundsPanel refreshKey={0} />)
    await screen.findByText('BK1')

    fireEvent.click(screen.getByRole('button', { name: 'Download CSV' }))

    await waitFor(() => expect(saveBlob).toHaveBeenCalledWith(blob, 'hoan-tien-20261112-1005.csv'))
    expect(await screen.findByText('Downloaded 2 refunds to hand to staff.')).toBeTruthy()
    expect(listPendingRefunds).toHaveBeenCalledTimes(2)           // tải lại để thấy "đã xuất"
  })

  it('shows an empty state and cannot download nothing', async () => {
    vi.mocked(listPendingRefunds).mockResolvedValue(page([]))
    render(<PendingRefundsPanel refreshKey={0} />)

    expect(await screen.findByText('No refunds are waiting for staff.')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Download CSV' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.queryByText('Select all')).toBeNull()
  })

  it('needs a selection and evidence before confirming', async () => {
    render(<PendingRefundsPanel refreshKey={0} />)
    await screen.findByText('BK1')
    const confirmButton = () => screen.getByRole('button', { name: /Confirm \d+ as refunded/ }) as HTMLButtonElement

    expect(confirmButton().disabled).toBe(true)
    fireEvent.click(screen.getByLabelText('Select refund BK1'))
    expect(confirmButton().disabled).toBe(true)                   // chưa có bằng chứng
    fireEvent.change(screen.getByLabelText('Evidence'), { target: { value: '   ' } })
    expect(confirmButton().disabled).toBe(true)                   // khoảng trắng không phải bằng chứng
    fireEvent.change(screen.getByLabelText('Evidence'), { target: { value: 'Đợt hoàn VNPay 12/11' } })
    expect(confirmButton().disabled).toBe(false)
  })

  it('confirms the selected refunds with the shared evidence and each row amount, then lists the failures', async () => {
    vi.mocked(confirmRefundsBatch).mockResolvedValue({
      confirmed: 1, failed: 1,
      items: [
        { refundTransactionId: 'r1', succeeded: true, status: 'Succeeded', error: null },
        { refundTransactionId: 'r2', succeeded: false, status: null, error: 'Số tiền đối soát không khớp khoản hoàn.' },
      ],
    })
    render(<PendingRefundsPanel refreshKey={0} />)
    await screen.findByText('BK1')

    fireEvent.click(screen.getByLabelText('Select all'))
    fireEvent.change(screen.getByLabelText('Evidence'), { target: { value: '  Đợt hoàn VNPay 12/11  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm 2 as refunded' }))

    await waitFor(() => expect(confirmRefundsBatch).toHaveBeenCalledWith([
      { refundTransactionId: 'r1', evidence: 'Đợt hoàn VNPay 12/11', gatewayReference: null, confirmedAmount: 40000 },
      { refundTransactionId: 'r2', evidence: 'Đợt hoàn VNPay 12/11', gatewayReference: null, confirmedAmount: 60000 },
    ]))
    expect(await screen.findByText('Confirmed 1, failed 1.')).toBeTruthy()
    expect(screen.getByText('Số tiền đối soát không khớp khoản hoàn.')).toBeTruthy()
    expect(listPendingRefunds).toHaveBeenCalledTimes(2)
  })

  it('shows the server message when the confirmation request itself fails', async () => {
    vi.mocked(confirmRefundsBatch).mockRejectedValue(new Error('down'))
    render(<PendingRefundsPanel refreshKey={0} />)
    await screen.findByText('BK1')

    fireEvent.click(screen.getByLabelText('Select refund BK1'))
    fireEvent.change(screen.getByLabelText('Evidence'), { target: { value: 'e' } })
    fireEvent.click(screen.getByRole('button', { name: 'Confirm 1 as refunded' }))

    expect(await screen.findByRole('alert')).toBeTruthy()
  })

  it('reloads when a refund is created elsewhere on the page', async () => {
    const { rerender } = render(<PendingRefundsPanel refreshKey={0} />)
    await screen.findByText('BK1')

    rerender(<PendingRefundsPanel refreshKey={1} />)

    await waitFor(() => expect(listPendingRefunds).toHaveBeenCalledTimes(2))
  })
})
