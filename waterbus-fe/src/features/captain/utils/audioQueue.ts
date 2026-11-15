/** One clip of an announcement; the server sends them in play order (Vietnamese first, then English). */
export interface AudioTriggerClip {
  language: 'vi' | 'en'
  /** Relative path, needs the captain's token (no public URL). */
  playbackPath: string
  contentType: string
  durationSeconds: number
  text: string
}

/** `audioTrigger` (version 2) pushed to the Onboard Player of the boat. */
export interface AudioTrigger {
  version: number
  requestId: string
  tripId: string
  boatId: string
  /** StationApproaching | StationArrived | StationDeparted | Poi */
  kind: string
  subjectId: string
  subjectName: string
  clips: AudioTriggerClip[]
  distanceMeters: number
  triggeredAt: string
}

export interface AudioQueue {
  /** Adds an announcement. False when its requestId was already received (the server retries with the same id). */
  enqueue: (trigger: AudioTrigger) => boolean
  next: () => AudioTrigger | undefined
  /** Drops what is waiting (e.g. pause from the server); received ids stay known so a retry is not played twice. */
  clear: () => void
  seen: (requestId: string) => boolean
  readonly size: number
}

export function createAudioQueue(): AudioQueue {
  const pending: AudioTrigger[] = []
  const seenIds = new Set<string>()
  return {
    enqueue(trigger) {
      if (seenIds.has(trigger.requestId)) return false
      seenIds.add(trigger.requestId)
      pending.push(trigger)
      return true
    },
    next: () => pending.shift(),
    clear: () => {
      pending.length = 0
    },
    seen: (requestId) => seenIds.has(requestId),
    get size() {
      return pending.length
    },
  }
}
