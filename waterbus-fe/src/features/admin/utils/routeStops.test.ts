import { describe, expect, it } from 'vitest'
import { stopsError } from './routeStops'
import type { RouteStopInput } from '../types'

const stop = (stationId: string): RouteStopInput => ({ stationId, canBoard: true, canAlight: true })

describe('stopsError', () => {
  it('accepts a regular route with distinct stations', () => {
    expect(stopsError([stop('a'), stop('b'), stop('c')], 'Regular')).toBeUndefined()
  })

  it('asks for a station on every stop', () => {
    expect(stopsError([stop('a'), stop('')], 'Regular')).toBeTruthy()
  })

  it('rejects a regular route that ends where it starts', () => {
    expect(stopsError([stop('a'), stop('b'), stop('a')], 'Regular')).toBeTruthy()
  })

  it('rejects a regular route that repeats a station', () => {
    expect(stopsError([stop('a'), stop('b'), stop('b'), stop('c')], 'Regular')).toBeTruthy()
  })

  it('lets a sightseeing route loop back to its start', () => {
    expect(stopsError([stop('a'), stop('b'), stop('a')], 'Sightseeing')).toBeUndefined()
  })
})
