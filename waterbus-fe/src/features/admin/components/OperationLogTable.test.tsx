import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import type { AdminTrip, OperationLog } from '../../operations/types'
import OperationLogTable from './OperationLogTable'

afterEach(cleanup)

const trip = {
  id: 't1',
  boatCode: 'SWB-02',
  departureTime: '2026-11-15T07:00:00Z',
} as AdminTrip

const log = (over: Partial<OperationLog>): OperationLog => ({
  id: 'e1',
  type: 'Suspend',
  tripId: 't1',
  reason: 'Storm',
  by: 'Admin',
  at: '2026-11-15T06:00:00Z',
  ...over,
})

describe('OperationLogTable', () => {
  it('names the trip by its boat and departure', () => {
    render(<OperationLogTable logs={[log({})]} trips={[trip]} />)
    expect(screen.getByText(/^SWB-02 • /)).toBeTruthy()
    expect(screen.getByText('Storm')).toBeTruthy()
  })

  it('shows a dash for a trip outside the operations window', () => {
    render(<OperationLogTable logs={[log({ tripId: 'old' })]} trips={[trip]} />)
    expect(screen.getAllByText('—').length).toBeGreaterThan(0)
  })

  it('says when nothing has been done yet', () => {
    render(<OperationLogTable logs={[]} trips={[]} />)
    expect(screen.getByText('Nothing has been done yet.')).toBeTruthy()
  })
})
