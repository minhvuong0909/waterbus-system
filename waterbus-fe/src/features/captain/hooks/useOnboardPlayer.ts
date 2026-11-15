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

/**
 * Onboard Player on the captain's screen: it plays what the server decides (the server owns when, the player only plays).
 * Each announcement is acknowledged at once, then its clips play one after another (Vietnamese, then English) and the
 * result is reported so the backend can audit it. Nothing is played or acknowledged until the captain taps "armed"
 * (browsers only allow sound after a tap).
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
        await playTrigger(trigger)
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
          const url = await fetchClipObjectUrl(clip.playbackPath)
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
          // Not armed: do not acknowledge, the server keeps the announcement for a later GPS tick (limited retries).
          if (!armedRef.current || q.seen(trigger.requestId)) return
          // Acknowledge within the server's timeout, then play in order.
          const accepted = await connection.current?.invoke<boolean>('AcceptPlayback', trigger.requestId)
          if (!accepted) return
          if (q.enqueue(trigger)) void drain()
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
  }, [])

  return { status, armed, arm, nowPlaying, log }
}
