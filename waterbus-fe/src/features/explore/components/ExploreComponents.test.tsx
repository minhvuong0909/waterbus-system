import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
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

  it('links the secondary hero action to the landmark section', () => {
    inRouter(<ExploreHero landmarks={heroLandmarks} />)
    expect(screen.getByRole('link', { name: /Explore landmarks/ }).getAttribute('href')).toBe(
      '#river-landmarks',
    )
  })

  it('rotates the landmark photos and lets visitors pause or choose one directly', () => {
    vi.useFakeTimers()
    inRouter(<ExploreHero landmarks={heroLandmarks} />)

    const firstPhoto = screen.getByRole('button', { name: 'Show background for Ba Son Waterfront' })
    const secondPhoto = screen.getByRole('button', { name: 'Show background for Thu Thiem Promenade' })
    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Ba Son Waterfront' })).toBeTruthy()

    act(() => vi.advanceTimersByTime(7000))
    expect(secondPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByRole('heading', { name: 'Thu Thiem Promenade' })).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Pause background rotation' }))
    fireEvent.click(firstPhoto)
    act(() => vi.advanceTimersByTime(7000))
    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')
  })

  it('does not auto-rotate when reduced motion is enabled', () => {
    reducedMotion = true
    vi.useFakeTimers()
    inRouter(<ExploreHero landmarks={heroLandmarks} />)

    const firstPhoto = screen.getByRole('button', { name: 'Show background for Ba Son Waterfront' })
    act(() => vi.advanceTimersByTime(7000))

    expect(firstPhoto.getAttribute('aria-pressed')).toBe('true')
    expect(screen.queryByRole('button', { name: 'Pause background rotation' })).toBeNull()
  })

  it('shows all landmarks without redundant category filters or action labels', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)

    expect(screen.queryByRole('group', { name: 'Filter by category' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Details: Ba Son Waterfront' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Details: Thu Thiem Promenade' })).toBeTruthy()
    expect(screen.queryByText('Explore', { exact: true })).toBeNull()
  })

  it('shows a clear empty state when no landmarks are available', () => {
    inRouter(<LandmarksSection landmarks={[]} />)

    expect(screen.getByText('No landmarks are available yet.')).toBeTruthy()
  })

  it('renders each landmark as an accessible scroll-reveal entry', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)

    landmarks.forEach((landmark) => {
      const entry = screen.getByRole('button', { name: `Details: ${landmark.title}` })
      expect(entry.getAttribute('data-reveal')).toBe('landmark')
      expect(entry.querySelector('h3')?.textContent).toBe(landmark.title)
      expect(entry.querySelector('[data-reveal-part="media"]')).toBeTruthy()
      expect(entry.querySelector('[data-reveal-part="copy"]')).toBeTruthy()
    })
  })

  it('opens the story in a side panel and gives focus back when it closes', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)
    const card = screen.getByRole('button', { name: /Ba Son Waterfront/ })
    card.focus()
    fireEvent.click(card)

    const dialog = screen.getByRole('dialog', { name: 'Ba Son Waterfront' })
    expect(dialog.textContent).toContain('Heritage meets public space.')
    expect(screen.queryByRole('link', { name: /Ba Son/ })).toBeNull()

    // Tab stays on the panel's close button instead of reaching the page behind it.
    fireEvent.keyDown(window, { key: 'Tab' })
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Close')

    fireEvent.keyDown(window, { key: 'Escape' })
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(document.activeElement).toBe(card)
  })

  it('shows a neutral tile instead of a photo of the wrong place', () => {
    inRouter(<LandmarksSection landmarks={landmarks} />)
    expect(screen.getByRole('img', { name: 'Ba Son Waterfront' })).toBeTruthy()
    expect(screen.queryByRole('img', { name: 'Thu Thiem Promenade' })).toBeNull()
  })
})
