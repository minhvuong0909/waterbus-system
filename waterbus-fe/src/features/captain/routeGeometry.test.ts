import { describe, expect, it } from 'vitest'
import { illustrationPoint, routeProgress } from './routeGeometry'
import type { LatLng } from '../../constant/riverPaths'

const path: LatLng[] = [
  [10, 106],
  [10, 107],
  [11, 107],
]
describe('Captain schematic GPS projection', () => {
  it('projects onto a river segment instead of inferring progress from elapsed time', () => {
    const progress = routeProgress([10, 106.5], path)
    expect(progress).toBeGreaterThan(0.2)
    expect(progress).toBeLessThan(0.3)
  })
  it('follows the served direction, including a reversed route', () => {
    expect(routeProgress(path[0], path)).toBe(0)
    expect(routeProgress(path[2], path)).toBe(1)
    expect(routeProgress(path[0], [...path].reverse())).toBe(1)
  })
  it('handles missing, repeated and out-of-bounds coordinates without invalid SVG values', () => {
    expect(routeProgress([0, 0], [])).toBe(0)
    expect(routeProgress(path[0], [path[0], path[0]])).toBe(0)
    expect(illustrationPoint(-1)).toEqual(illustrationPoint(0))
    expect(illustrationPoint(2)).toEqual(illustrationPoint(1))
  })
})
