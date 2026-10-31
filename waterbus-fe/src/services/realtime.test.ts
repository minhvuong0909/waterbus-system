import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const fake = vi.hoisted(() => {
  const state = {
    startResults: [] as ('ok' | 'fail')[],
    onclose: undefined as (() => void) | undefined,
    onreconnected: undefined as (() => Promise<void>) | undefined,
  }
  return state
})

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() { return this }
    withAutomaticReconnect() { return this }
    configureLogging() { return this }
    build() {
      return {
        state: 'Disconnected',
        on: vi.fn(),
        onreconnecting: vi.fn(),
        onreconnected: (cb: () => Promise<void>) => { fake.onreconnected = cb },
        onclose: (cb: () => void) => { fake.onclose = cb },
        start: vi.fn(async () => {
          if (fake.startResults.shift() === 'fail') throw new Error('offline')
        }),
        stop: vi.fn(async () => undefined),
      }
    }
  }
  return {
    HubConnectionBuilder,
    HubConnectionState: { Disconnected: 'Disconnected' },
    LogLevel: { Warning: 3 },
  }
})

import { connectHub } from './realtime'

const flush = () => vi.advanceTimersByTimeAsync(0)

beforeEach(() => {
  vi.useFakeTimers()
  fake.startResults = []
  fake.onclose = undefined
  fake.onreconnected = undefined
})
afterEach(() => {
  vi.useRealTimers()
})

describe('connectHub recovery refresh', () => {
  it('does not refresh on the first successful connection', async () => {
    const onReconnected = vi.fn()
    const stop = connectHub('/hubs/x', { handlers: {}, onReconnected })
    await flush()
    expect(onReconnected).not.toHaveBeenCalled()
    stop()
  })

  it('refreshes once, after rejoining groups, when a manual restart succeeds after reconnect gave up', async () => {
    const order: string[] = []
    const stop = connectHub('/hubs/x', {
      handlers: {},
      onConnected: async () => { order.push('rejoin') },
      onReconnected: () => { order.push('refresh') },
    })
    await flush()
    order.length = 0

    fake.onclose?.() // automatic reconnect exhausted
    await vi.advanceTimersByTimeAsync(5000) // START_RETRY_MS
    expect(order).toEqual(['rejoin', 'refresh'])
    stop()
  })

  it('does not refresh when the very first connect only succeeds on a retry', async () => {
    fake.startResults = ['fail']
    const onReconnected = vi.fn()
    const stop = connectHub('/hubs/x', { handlers: {}, onReconnected })
    await flush()
    await vi.advanceTimersByTimeAsync(5000)
    expect(onReconnected).not.toHaveBeenCalled()
    stop()
  })

  it('keeps the connection live when the refresh itself throws', async () => {
    const statuses: string[] = []
    const stop = connectHub('/hubs/x', {
      handlers: {},
      onStatus: (s) => statuses.push(s),
      onReconnected: () => { throw new Error('REST down') },
    })
    await flush()
    fake.onclose?.()
    await vi.advanceTimersByTimeAsync(5000)
    expect(statuses.at(-1)).toBe('live')
    stop()
  })
})
