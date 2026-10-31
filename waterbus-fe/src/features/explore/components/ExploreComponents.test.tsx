import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import type { Journey, Landmark } from '../types'
import ExploreHero from './ExploreHero'
import JourneysSection from './JourneysSection'
import LandmarksSection from './LandmarksSection'
import { SIGHTSEEING_RESULTS } from '../constants'
import StepAboardBanner from './StepAboardBanner'

const journey = (id: string, route = 'Bach Dang Pier ➔ Linh Dong Pier'): Journey => ({
  id, image: `${id}.jpg`, tags: ['Scenic Route', 'Skyline Views'], title: `Title ${id}`, description: 'd', route,
})
const landmarks: Landmark[] = [
  { id: 'ba-son', image: 'ba-son.jpg', category: 'Heritage', title: 'Ba Son Waterfront', description: 'Heritage meets public space.' },
  { id: 'thu-thiem', category: 'Skyline', title: 'Thu Thiem Promenade', description: 'Skyline views.' },
]
const inRouter = (ui: JSX.Element) => render(<MemoryRouter>{ui}</MemoryRouter>)

describe('Explore page', () => {
  afterEach(cleanup)

  it('sends both calls to action to the sightseeing departures', () => {
    inRouter(<><ExploreHero /><StepAboardBanner /></>)
    const links = screen.getAllByRole('link', { name: /See sightseeing trips/ })
    expect(links).toHaveLength(2)
    links.forEach((link) => expect(link.getAttribute('href')).toBe(SIGHTSEEING_RESULTS))
    expect(SIGHTSEEING_RESULTS).toBe('/search/results?type=sightseeing')
  })

  it('says a route shared by every featured route once, not on each card', () => {
    inRouter(<JourneysSection journeys={[journey('a'), journey('b'), journey('c')]} />)
    expect(screen.getAllByText(/Bach Dang Pier ➔ Linh Dong Pier/)).toHaveLength(1)
    expect(screen.queryByText('Explore Journey')).toBeNull()
  })

  it('keeps the route on each card when the routes differ', () => {
    inRouter(<JourneysSection journeys={[journey('a'), journey('b', 'Bach Dang Pier ➔ Binh An Pier')]} />)
    expect(screen.getByText('Bach Dang Pier ➔ Linh Dong Pier')).toBeTruthy()
    expect(screen.getByText('Bach Dang Pier ➔ Binh An Pier')).toBeTruthy()
  })

  it('filters with pressed buttons and shows a note for an empty category', () => {
    inRouter(<LandmarksSection categories={['Skyline', 'Heritage', 'Culture']} landmarks={landmarks} />)
    const culture = screen.getByRole('button', { name: 'Culture' })
    fireEvent.click(culture)
    expect(culture.getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('No places in this category yet.')).toBeTruthy()
    expect(screen.queryByRole('tab')).toBeNull()
  })

  it('opens the story in a side panel and gives focus back when it closes', () => {
    inRouter(<LandmarksSection categories={['Skyline', 'Heritage']} landmarks={landmarks} />)
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
    inRouter(<LandmarksSection categories={['Skyline']} landmarks={landmarks} />)
    expect(screen.getByRole('img', { name: 'Ba Son Waterfront' })).toBeTruthy()
    expect(screen.queryByRole('img', { name: 'Thu Thiem Promenade' })).toBeNull()
  })
})
