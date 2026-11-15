import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import LinkOrdersCard from './LinkOrdersCard'

const fetchMock = vi.fn()
beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  cleanup()
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('link past orders by verified email', () => {
  it('shows nothing when there are no orders to add', async () => {
    fetchMock.mockResolvedValue(json({ count: 0 }))
    const { container } = render(<LinkOrdersCard onLinked={vi.fn()} />)

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())

    expect(container.textContent).toBe('')
  })

  it('links nothing until the traveler presses the button', async () => {
    fetchMock.mockResolvedValue(json({ count: 3 }))
    render(<LinkOrdersCard onLinked={vi.fn()} />)

    expect(await screen.findByRole('button', { name: /Add to my account|Thêm vào tài khoản của tôi/i })).toBeTruthy()

    expect(fetchMock).toHaveBeenCalledTimes(1) // chỉ lần đếm, chưa có lần gắn
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/me\/orders\/linkable$/)
  })

  it('keeps linking while orders remain, then refreshes the list', async () => {
    const onLinked = vi.fn()
    fetchMock
      .mockResolvedValueOnce(json({ count: 250 }))
      .mockResolvedValueOnce(json({ linked: 200, remaining: 50 }))
      .mockResolvedValueOnce(json({ linked: 50, remaining: 0 }))
      .mockResolvedValue(json({ count: 0 }))
    render(<LinkOrdersCard onLinked={onLinked} />)

    fireEvent.click(await screen.findByRole('button', { name: /Add to my account|Thêm vào tài khoản của tôi/i }))

    await waitFor(() => expect(onLinked).toHaveBeenCalledTimes(1))
    const posts = fetchMock.mock.calls.filter((c) => String(c[0]).endsWith('/me/orders/link-by-email'))
    expect(posts).toHaveLength(2)
    expect(posts.every((c) => c[1].method === 'POST')).toBe(true)
  })

  it('tells the traveler to confirm the email first when the backend refuses', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ count: 2 }))
      .mockResolvedValueOnce(json({ title: 'x', status: 403, detail: 'x' }, 403))
    render(<LinkOrdersCard onLinked={vi.fn()} />)

    fireEvent.click(await screen.findByRole('button', { name: /Add to my account|Thêm vào tài khoản của tôi/i }))

    expect((await screen.findByRole('alert')).textContent).toMatch(/Confirm your email first|xác minh email trước/i)
  })
})
