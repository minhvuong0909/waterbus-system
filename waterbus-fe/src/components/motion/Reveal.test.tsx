import { act, cleanup, render } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import Reveal from './Reveal'

describe('Reveal', () => {
  const observe = vi.fn()
  const unobserve = vi.fn()
  const disconnect = vi.fn()
  const cancel = vi.fn()
  const animate = vi.fn(() => ({ cancel, onfinish: null, oncancel: null }))
  let intersect: (target: Element) => void
  let change: () => void
  let reduced = false

  beforeEach(() => {
    vi.clearAllMocks()
    reduced = false
    Object.defineProperty(Element.prototype, 'animate', { configurable: true, value: animate })
    vi.stubGlobal('matchMedia', () => ({
      get matches() {
        return reduced
      },
      addEventListener: (_: string, listener: () => void) => {
        change = listener
      },
      removeEventListener: vi.fn(),
    }))
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        constructor(callback: IntersectionObserverCallback) {
          intersect = (target) =>
            callback(
              [
                {
                  target,
                  isIntersecting: true,
                  boundingClientRect: { top: 100 },
                } as IntersectionObserverEntry,
              ],
              this as unknown as IntersectionObserver,
            )
        }
        observe = observe
        unobserve = unobserve
        disconnect = disconnect
      },
    )
  })
  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    delete (Element.prototype as Partial<Element>).animate
  })

  it('observes only marked content inside its own container and releases entered elements', () => {
    const { getByText } = render(
      <>
        <p data-reveal>Outside</p>
        <Reveal>
          <p data-reveal>Inside</p>
        </Reveal>
      </>,
    )
    expect(observe).toHaveBeenCalledTimes(1)
    expect(observe).toHaveBeenCalledWith(getByText('Inside'))
    act(() => intersect(getByText('Inside')))
    expect(unobserve).toHaveBeenCalledWith(getByText('Inside'))
    expect(animate).toHaveBeenCalledTimes(1)
    act(() => intersect(getByText('Inside')))
    expect(animate).toHaveBeenCalledTimes(1)
  })

  it('keeps content readable without IntersectionObserver', () => {
    vi.stubGlobal('IntersectionObserver', undefined)
    const { getByText } = render(
      <Reveal>
        <p data-reveal>Readable</p>
      </Reveal>,
    )
    expect(getByText('Readable').style.opacity).toBe('')
    expect(animate).not.toHaveBeenCalled()
  })

  it('skips motion when reduced motion is enabled and cancels it on a live preference change', () => {
    reduced = true
    const { getByText } = render(
      <Reveal>
        <p data-reveal>Content</p>
      </Reveal>,
    )
    expect(observe).not.toHaveBeenCalled()
    act(() => {
      reduced = false
      change()
    })
    act(() => intersect(getByText('Content')))
    expect(animate).toHaveBeenCalledTimes(1)
    act(() => {
      reduced = true
      change()
    })
    expect(cancel).toHaveBeenCalledTimes(1)
  })

  it('disconnects observers and cancels active animation on unmount', () => {
    const { getByText, unmount } = render(
      <Reveal>
        <p data-reveal>Content</p>
      </Reveal>,
    )
    act(() => intersect(getByText('Content')))
    disconnect.mockClear()
    unmount()
    expect(disconnect).toHaveBeenCalledTimes(1)
    expect(cancel).toHaveBeenCalledTimes(1)
  })
})
