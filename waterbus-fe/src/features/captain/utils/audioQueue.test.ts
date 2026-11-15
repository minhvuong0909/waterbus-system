import { describe, expect, it } from 'vitest'
import { createAudioQueue } from './audioQueue'
import type { AudioTrigger } from './audioQueue'

const trigger = (requestId: string): AudioTrigger => ({
  version: 2,
  requestId,
  tripId: 't',
  boatId: 'b',
  kind: 'StationArrived',
  subjectId: 's',
  subjectName: 'Bến Linh Đông',
  clips: [],
  distanceMeters: 10,
  triggeredAt: '2026-01-01T00:00:00Z',
})

describe('audioQueue', () => {
  it('plays announcements in the order they arrive', () => {
    const queue = createAudioQueue()
    queue.enqueue(trigger('a'))
    queue.enqueue(trigger('b'))
    expect(queue.next()?.requestId).toBe('a')
    expect(queue.next()?.requestId).toBe('b')
    expect(queue.next()).toBeUndefined()
  })

  it('refuses a requestId it already received, so a server retry is not played twice', () => {
    const queue = createAudioQueue()
    expect(queue.enqueue(trigger('a'))).toBe(true)
    expect(queue.enqueue(trigger('a'))).toBe(false)
    expect(queue.size).toBe(1)
  })

  it('clear drops what waits but still remembers received ids', () => {
    const queue = createAudioQueue()
    queue.enqueue(trigger('a'))
    queue.clear()
    expect(queue.size).toBe(0)
    expect(queue.enqueue(trigger('a'))).toBe(false)
  })
})
