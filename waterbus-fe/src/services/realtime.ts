import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { API_BASE_URL } from '../constant/env'
import { getToken } from './authStorage'

export type RealtimeStatus = 'connecting' | 'live' | 'reconnecting'

export interface HubOptions {
  /** Event name → handler for what the server pushes. */
  handlers: Record<string, (payload: never) => void>
  /**
   * Runs after the first connect and after every reconnect. Join groups here: the server forgets
   * them when the connection drops.
   */
  onConnected?: (connection: HubConnection) => Promise<void>
  /** Runs after a reconnect, to reload with REST what was pushed while the connection was down. */
  onReconnected?: () => void
  onStatus?: (status: RealtimeStatus) => void
}

const RETRY_DELAYS_MS = [0, 2000, 5000, 10000, 30000]
const START_RETRY_MS = 5000

/**
 * Opens a SignalR connection to `hubPath` with the signed-in user's token (if any) and keeps it up:
 * automatic reconnects, a retry loop for the first connect, and re-joining groups each time.
 * @returns a function that closes the connection
 */
export function connectHub(hubPath: string, options: HubOptions): () => void {
  const { handlers, onConnected, onReconnected, onStatus } = options
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}${hubPath}`, { accessTokenFactory: () => getToken() ?? '' })
    .withAutomaticReconnect(RETRY_DELAYS_MS)
    .configureLogging(LogLevel.Warning)
    .build()

  Object.entries(handlers).forEach(([event, handler]) =>
    connection.on(event, handler as (...args: unknown[]) => void),
  )

  let stopped = false
  let retryTimer: number | undefined
  let hasConnected = false

  // The refresh reloads data with REST; if it fails the connection is still fine, so it must not trigger a reconnect.
  const refresh = () => {
    try {
      onReconnected?.()
    } catch {
      // The next reconnect or a manual reload will try again.
    }
  }

  connection.onreconnecting(() => onStatus?.('reconnecting'))
  connection.onreconnected(async () => {
    await onConnected?.(connection)
    onStatus?.('live')
    refresh()
  })
  // Automatic reconnect gave up: start over with the retry loop.
  connection.onclose(() => {
    if (!stopped) {
      onStatus?.('reconnecting')
      retryTimer = window.setTimeout(start, START_RETRY_MS)
    }
  })

  async function start() {
    if (stopped) return
    onStatus?.('connecting')
    try {
      await connection.start()
      if (stopped) return
      await onConnected?.(connection)
      onStatus?.('live')
      // A start after the connection was lost for good (automatic reconnect gave up) missed events: reload like onreconnected.
      if (hasConnected) refresh()
      hasConnected = true
    } catch {
      if (!stopped && connection.state === HubConnectionState.Disconnected) {
        retryTimer = window.setTimeout(start, START_RETRY_MS)
      }
    }
  }

  void start()

  return () => {
    stopped = true
    window.clearTimeout(retryTimer)
    void connection.stop()
  }
}

/** Message of a `HubException` thrown by a hub method, without the SignalR wrapper text. */
export function hubErrorMessage(error: unknown): string {
  const message = error instanceof Error ? error.message : String(error)
  const marker = 'HubException: '
  const index = message.indexOf(marker)
  return index >= 0 ? message.slice(index + marker.length) : message
}
