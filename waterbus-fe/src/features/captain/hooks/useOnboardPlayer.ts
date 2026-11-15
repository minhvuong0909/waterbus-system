import { useCallback, useEffect, useRef, useState } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import type { RealtimeStatus } from '../../../services/realtime'
import { fetchClipObjectUrl, playAudioUrl } from '../services/onboardAudioService'
import { createAudioQueue } from '../utils/audioQueue'
import type { AudioTrigger } from '../utils/audioQueue'

interface AudioControl {
  action: 'pause' | 'recheck'
  reason: string
}

export interface NowPlaying {
  subjectName: string
  language: string
  text: string
}

const MAX_LOG = 6

/** An announcement waiting longer than this is no longer true (the boat has passed), so it is skipped. */
const STALE_MS = 90_000

/**
 * Onboard Player on the captain's screen: it plays what the server decides (the server owns when, the player only plays).
 * Each announcement is acknowledged at once, then its clips play one after another (Vietnamese, then English) and the
 * result is reported so the backend can audit it. Browsers only allow sound after a tap, so the player arms itself on the
 * first tap or key press anywhere on the page (the captain always presses something before a trip). Announcements that
 * arrive before that are acknowledged and queued, then played on arming unless they have gone stale.
 */
export function useOnboardPlayer(enabled: boolean) {
  const [status, setStatus] = useState<RealtimeStatus>('connecting')
  const [armed, setArmed] = useState(false)
  const [nowPlaying, setNowPlaying] = useState<NowPlaying | null>(null)
  const [log, setLog] = useState<string[]>([])
  const armedRef = useRef(false)
  const connection = useRef<HubConnection | null>(null)
  const queue = useRef(createAudioQueue())
  const playing = useRef(false)
  const stopCurrent = useRef<(() => void) | null>(null)
  const receivedAt = useRef(new Map<string, number>())

  const addLog = useCallback((line: string) => {
    setLog((previous) => [`${new Date().toLocaleTimeString('vi-VN')} ${line}`, ...previous].slice(0, MAX_LOG))
  }, [])

  const report = useCallback(async (requestId: string, state: 'Started' | 'Completed' | 'Failed', message?: string) => {
    try {
      await connection.current?.invoke('ReportPlaybackState', requestId, state, message ?? null)
    } catch {
      // The report is only for audit; playback must not depend on it.
    }
  }, [])

  const drain = useCallback(async () => {
    if (playing.current) return
    playing.current = true
    try {
      let trigger = queue.current.next()
      while (trigger) {
        const waited = Date.now() - (receivedAt.current.get(trigger.requestId) ?? Date.now())
        if (waited > STALE_MS) {
          addLog(`${trigger.subjectName}: bỏ qua (quá hạn)`)
          await report(trigger.requestId, 'Failed', 'Stale')
        } else {
          await playTrigger(trigger)
        }
        trigger = queue.current.next()
      }
    } finally {
      playing.current = false
      setNowPlaying(null)
    }

    async function playTrigger(item: AudioTrigger) {
      await report(item.requestId, 'Started')
      try {
        for (const clip of item.clips) {
          const url = await fetchClipObjectUrl(item.requestId, clip.language)
          setNowPlaying({ subjectName: item.subjectName, language: clip.language, text: clip.text })
          addLog(`${item.subjectName} (${clip.language})`)
          try {
            await playAudioUrl(url, (stop) => {
              stopCurrent.current = stop
            })
          } finally {
            stopCurrent.current = null
            URL.revokeObjectURL(url)
          }
        }
        await report(item.requestId, 'Completed')
      } catch (error) {
        addLog(`${item.subjectName}: ${error instanceof Error ? error.message : 'error'}`)
        await report(item.requestId, 'Failed', error instanceof Error ? error.message : undefined)
      }
    }
  }, [addLog, report])

  useEffect(() => {
    if (!enabled) return
    const q = queue.current
    return connectHub(HUBS.onboardAudio, {
      handlers: {
        [HUB_EVENTS.audioTrigger]: async (trigger: AudioTrigger) => {
          if (q.seen(trigger.requestId)) return
          // Acknowledge at once (within the server's timeout) even before the first tap, so the server does not use up its
          // retries; the announcement waits in the queue until the speaker is armed.
          const accepted = await connection.current?.invoke<boolean>('AcceptPlayback', trigger.requestId)
          if (!accepted) return
          if (q.enqueue(trigger)) {
            receivedAt.current.set(trigger.requestId, Date.now())
            if (armedRef.current) void drain()
          }
        },
        [HUB_EVENTS.audioControl]: (control: AudioControl) => {
          if (control.action !== 'pause') return
          q.clear()
          stopCurrent.current?.()
          addLog(`pause: ${control.reason}`)
        },
      },
      onConnected: async (c) => {
        connection.current = c
      },
      onStatus: setStatus,
    })
  }, [enabled, drain, addLog])

  const arm = useCallback(() => {
    armedRef.current = true
    setArmed(true)
    void drain()
  }, [drain])

  // First tap or key press anywhere: browsers now allow sound.
  useEffect(() => {
    if (!enabled || armed) return
    const unlock = () => arm()
    window.addEventListener('pointerdown', unlock, { once: true })
    window.addEventListener('keydown', unlock, { once: true })
    return () => {
      window.removeEventListener('pointerdown', unlock)
      window.removeEventListener('keydown', unlock)
    }
  }, [enabled, armed, arm])

  return { status, armed, arm, nowPlaying, log }
}
