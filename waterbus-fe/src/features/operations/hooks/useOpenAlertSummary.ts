import { useEffect, useMemo } from 'react'
import { useFetch, useReload } from '../../../hooks'
import { getOpenIncidents } from '../services/operationsService'
import { useFleetHub } from './useFleetHub'

/** How many alerts are open and how many trips still wait for the admin's decision. */
export interface OpenAlertSummary {
  open: number
  waiting: number
  /** 2 when any open alert says to stop operations, otherwise 1. */
  level: 1 | 2
}

/** The backend checks the weather on a schedule; this is only the safety net should a push be missed. */
const POLL_MS = 60_000

/**
 * Open alerts of the whole fleet, kept current for an admin on ANY page: a REST snapshot, a refresh on every push
 * (weather alert, emergency, trip status change, reconnect) and a slow poll. Null until the first load, when the
 * list cannot be read, and when nothing is open.
 */
export function useOpenAlertSummary(enabled: boolean): OpenAlertSummary | null {
  const [version, reload] = useReload()
  const alerts = useFetch(() => (enabled ? getOpenIncidents(false) : Promise.resolve([])), [enabled, version])
  useFleetHub({ onAlert: reload, onIncident: reload, onTripStatus: reload, onReconnected: reload }, enabled)

  useEffect(() => {
    if (!enabled) return
    const timer = window.setInterval(reload, POLL_MS)
    return () => window.clearInterval(timer)
  }, [enabled, reload])

  return useMemo(() => {
    const open = alerts.data ?? []
    if (open.length === 0) return null
    return {
      open: open.length,
      waiting: open.reduce((sum, alert) => sum + (alert.pendingDecisionCount ?? 0), 0),
      level: open.some((alert) => alert.level === 2) ? 2 : 1,
    }
  }, [alerts.data])
}
