import { useEffect, useState } from 'react'
import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { useFetch } from '../../../hooks'
import { getToken } from '../../../services/authStorage'
import { connectHub } from '../../../services/realtime'
import { getTrackedTrip } from '../services/trackingService'
import type { BoatFix } from '../types'

/** Payload of the `boatPosition` hub event (docs/REALTIME_CONTRACT.md). */
interface BoatPositionEvent {
  boatId: string
  lat: number
  lng: number
  speedKnots: number | null
  observedAt: string
}

/**
 * The trip a traveler follows, kept current:
 * - its status from `tripStatusChanged` on the seat-sync hub (group `trip:{id}`, open to guests),
 *   reloading the trip with REST each time and after a reconnect;
 * - the boat's position from the boat-tracking hub (group `boat:{id}`), which requires a sign-in.
 */
export function useTripTracking(tripId: string) {
  const [version, setVersion] = useState(0)
  const trip = useFetch(() => getTrackedTrip(tripId), [tripId, version])
  // Tagged with its boat, so a fix of a previous boat is never shown for another one.
  const [fix, setFix] = useState<{ boatId: string; position: BoatFix } | null>(null)
  const boatId = trip.data?.boat?.boatId
  const position = fix && fix.boatId === boatId ? fix.position : null
  const signedIn = getToken() !== null

  useEffect(() => {
    const reload = () => setVersion((v) => v + 1)
    return connectHub(HUBS.seatSync, {
      handlers: { [HUB_EVENTS.tripStatusChanged]: reload },
      onConnected: (connection) => connection.invoke('JoinTrip', tripId),
      onReconnected: reload,
    })
  }, [tripId])

  useEffect(() => {
    if (!boatId || !signedIn) return
    return connectHub(HUBS.boatTracking, {
      handlers: {
        [HUB_EVENTS.boatPosition]: (event: BoatPositionEvent) => {
          if (event.boatId !== boatId) return
          setFix({
            boatId,
            position: { latitude: event.lat, longitude: event.lng, speedKnots: event.speedKnots, observedAt: event.observedAt },
          })
        },
      },
      onConnected: (connection) => connection.invoke('JoinBoat', boatId),
    })
  }, [boatId, signedIn])

  return { ...trip, position, signedIn }
}
