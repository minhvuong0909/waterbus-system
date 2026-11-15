import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../services/apiClient'
import ConfirmModal from './ConfirmModal'

afterEach(cleanup)

const props = () => ({
  title: 'Close this alert?',
  message: 'Closing only ends the alert.',
  confirmLabel: 'Close alert',
  onDone: vi.fn(),
  onClose: vi.fn(),
})

describe('ConfirmModal', () => {
  it('runs the action and reports done', async () => {
    const p = props()
    const action = vi.fn().mockResolvedValue(undefined)
    render(<ConfirmModal {...p} action={action} />)
    fireEvent.click(screen.getByRole('button', { name: 'Close alert' }))
    await waitFor(() => expect(p.onDone).toHaveBeenCalled())
    expect(action).toHaveBeenCalledTimes(1)
  })

  it('stays open with the reason when the server refuses', async () => {
    const p = props()
    const refusal = Object.assign(Object.create(ApiError.prototype), { status: 409, message: 'Already closed.', fieldErrors: {} })
    render(<ConfirmModal {...p} action={vi.fn().mockRejectedValue(refusal)} />)
    fireEvent.click(screen.getByRole('button', { name: 'Close alert' }))
    expect((await screen.findByRole('alert')).textContent).toBe('Already closed.')
    expect(p.onDone).not.toHaveBeenCalled()
  })

  it('shows extra content and closes without acting on cancel', () => {
    const p = props()
    const action = vi.fn()
    render(
      <ConfirmModal {...p} action={action}>
        <p>3 trips still have no decision.</p>
      </ConfirmModal>,
    )
    expect(screen.getByText('3 trips still have no decision.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
    expect(p.onClose).toHaveBeenCalled()
    expect(action).not.toHaveBeenCalled()
  })
})
