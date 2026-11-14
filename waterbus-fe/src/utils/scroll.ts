/** How long to keep putting the page back while its content is still loading. */
const HOLD_MS = 3000
const CHECK_EVERY_MS = 50

const USER_SCROLL_EVENTS = ['wheel', 'touchstart', 'keydown', 'pointerdown'] as const

/**
 * Scrolls back to `y` and keeps it there while the page re-renders and its data loads: the page is
 * shorter until then, so a single scroll would stop early, and a freshly mounted page scrolls itself
 * to the top. Stops as soon as the visitor scrolls themself, or after a few seconds. Returns a
 * function that stops it early.
 */
export function holdScrollPosition(y: number): () => void {
  const restore = () => {
    if (Math.abs(window.scrollY - y) > 1) window.scrollTo(0, y)
  }
  restore()

  const interval = window.setInterval(restore, CHECK_EVERY_MS)
  const timer = window.setTimeout(stop, HOLD_MS)
  USER_SCROLL_EVENTS.forEach((event) => window.addEventListener(event, stop, { passive: true }))

  function stop() {
    window.clearInterval(interval)
    window.clearTimeout(timer)
    USER_SCROLL_EVENTS.forEach((event) => window.removeEventListener(event, stop))
  }
  return stop
}
