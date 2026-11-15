import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import CountUp from './CountUp'

beforeEach(() => {
  vi.stubGlobal(
    'matchMedia',
    () => ({ matches: false, addEventListener: () => undefined, removeEventListener: () => undefined }),
  )
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('CountUp', () => {
  it('shows the full text when the browser cannot observe visibility', () => {
    vi.stubGlobal('IntersectionObserver', undefined)
    render(<CountUp value="Từ 15.000 VND" />)
    expect(screen.getByText('Từ 15.000 VND')).toBeTruthy()
  })

  it('shows the final text from the start for text without a number', () => {
    render(<CountUp value="Bạch Đằng" />)
    expect(screen.getByText('Bạch Đằng')).toBeTruthy()
  })

  it('keeps the real text readable while the count runs', () => {
    class Observer {
      constructor(private callback: IntersectionObserverCallback) {}
      observe() {
        this.callback([{ isIntersecting: true } as IntersectionObserverEntry], this as unknown as IntersectionObserver)
      }
      disconnect() {}
    }
    vi.stubGlobal('IntersectionObserver', Observer)
    render(<CountUp value="45 phút" />)
    // Chỗ giữ cỡ luôn chứa chuỗi cuối cùng nên bố cục và trình đọc màn hình không đổi.
    expect(screen.getByText('45 phút')).toBeTruthy()
  })
})
