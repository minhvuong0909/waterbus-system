import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Landmark } from '../types'
import ExploreHero from './ExploreHero'
import LandmarksSection from './LandmarksSection'
import { SIGHTSEEING_RESULTS } from '../constants'

const landmarks: Landmark[] = [
  { id: 'ba-son', image: 'ba-son.jpg', category: 'Heritage', title: 'Ba Son Waterfront', description: 'Heritage meets public space.' },
  { id: 'thu-thiem', category: 'Skyline', title: 'Thu Thiem Promenade', description: 'Skyline views.' },
]
const heroLandmarks: Landmark[] = [
  landmarks[0],
  { ...landmarks[1], image: 'thu-thiem.jpg' },
]
const inRouter = (ui: JSX.Element) => render(<MemoryRouter>{ui}</MemoryRouter>)
let reducedMotion = false

describe('Explore page', () => {
  beforeEach(() => {
    reducedMotion = false
    vi.stubGlobal('matchMedia', () => ({
      get matches() {
        return reducedMotion
      },
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }))
  })

  afterEach(() => {
    cleanup()
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  it('sends the call to action to the sightseeing departures', () => {
    inRouter(<ExploreHero landmarks={heroLandmarks} />)
    const link = screen.getByRole('link', { name: /See sightseeing trips/ })
    expect(link.getAttribute('href')).toBe(SIGHTSEEING_RESULTS)
    expect(SIGHTSEEING_RESULTS).toBe('/search/results?type=sightseeing')
  })

  it('moves to the next landmark when the progress of the current one ends, and lets visitors choose one', () => {
    const { container } = inRouter(<ExploreHero landmarks={heroLandmarks} />)
    const progress = () => container.querySelector('[data-hero-progress]') as HTMLElement

    const firstPhoto = screen.getByRole('button', { name: 'Show background for Ba Son Waterfront' })
    const secondPhoto = screen.getByRole('button', { name: 'Show background for Thu Thiem Promenade' })
    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Ba Son Waterfront' })).toBeTruthy()

    fireEvent.animationEnd(progress())
    expect(secondPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Thu Thiem Promenade' })).toBeTruthy()

    fireEvent.animationEnd(progress())
    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')

    expect(progress().style.animationPlayState).toBe('running')
    fireEvent.mouseEnter(screen.getByRole('heading', { name: 'Ba Son Waterfront' }).parentElement as HTMLElement)
    expect(progress().style.animationPlayState).toBe('paused')
    fireEvent.click(secondPhoto)
    expect(secondPhoto.getAttribute('aria-pressed')).toBe('true')
  })

  it('opens on the landmark picked elsewhere, and on the first one when the id is unknown', () => {
    inRouter(<ExploreHero landmarks={heroLandmarks} startId="thu-thiem" />)
    expect(screen.getByRole('heading', { name: 'Thu Thiem Promenade' })).toBeTruthy()
    cleanup()
    inRouter(<ExploreHero landmarks={heroLandmarks} startId="nowhere" />)
    expect(screen.getByRole('heading', { name: 'Ba Son Waterfront' })).toBeTruthy()
  })

  it('does not auto-rotate when reduced motion is enabled', () => {
    reducedMotion = true
    const { container } = inRouter(<ExploreHero landmarks={heroLandmarks} />)

    const firstPhoto = screen.getByRole('button', { name: 'Show background for Ba Son Waterfront' })
    const progress = container.querySelector('[data-hero-progress]') as HTMLElement
    fireEvent.animationEnd(progress)

    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(progress.className).not.toContain('animate-hero-progress')
  })

  it('shows all landmarks without redundant category filters or action labels', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)

    expect(screen.queryByRole('group', { name: 'Filter by category' })).toBeNull()
    expect(screen.queryByRole('button', { name: /Ba Son Waterfront|Thu Thiem Promenade/ })).toBeNull()
    expect(screen.queryByText('Explore', { exact: true })).toBeNull()
  })

  it('shows a clear empty state when no landmarks are available', () => {
    inRouter(<LandmarksSection landmarks={[]} />)

    expect(screen.getByText('No landmarks are available yet.')).toBeTruthy()
  })

  it('renders each landmark as an accessible scroll-reveal entry', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)

    landmarks.forEach((landmark) => {
      const heading = screen.getByRole('heading', { level: 3, name: landmark.title })
      const entry = heading.closest('[data-reveal="landmark"]')

      expect(entry?.tagName).toBe('ARTICLE')
      expect(entry?.getAttribute('data-reveal')).toBe('landmark')
      expect(entry?.querySelector('[data-reveal-part="media"]')).toBeTruthy()
      expect(entry?.querySelector('[data-reveal-part="copy"]')).toBeTruthy()
    })
  })

  it('does not open a detail panel when a landmark is clicked', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)
    expect(screen.queryByRole('dialog')).toBeNull()
    fireEvent.click(screen.getByRole('heading', { level: 3, name: 'Ba Son Waterfront' }))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Close' })).toBeNull()
  })

  it('shows a neutral tile instead of a photo of the wrong place', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)
    expect(screen.getByRole('img', { name: 'Ba Son Waterfront' })).toBeTruthy()
    expect(screen.queryByRole('img', { name: 'Thu Thiem Promenade' })).toBeNull()
  })
})
