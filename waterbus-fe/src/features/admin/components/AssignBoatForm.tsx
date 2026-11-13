import { useState } from 'react'
import type { FormEvent } from 'react'
import { SelectField } from '../../../components/ui'
import { t } from '../../../i18n'
import { formatDateTime } from '../../../utils/time'
import { useSubmit } from '../../../hooks'
import { assignTripBoat } from '../services/tripPlanningService'
import type { AdminBoat, AssignBoatResult } from '../types'
import type { AdminTrip } from '../../operations/types'
import FormActions from './FormActions'

/**
 * Gives a `Scheduled` trip its boat or replaces it (BR-SCH-04). The backend refuses a boat that
 * runs an overlapping trip (BR-SCH-08) or cannot seat what was already sold (BR-SCH-07); its
 * reason is shown as it is.
 */
export default function AssignBoatForm({
  trip,
  boats,
  onDone,
  onCancel,
}: {
  trip: AdminTrip
  boats: AdminBoat[]
  onDone: (result: AssignBoatResult) => void
  onCancel: () => void
}) {
  const [boatId, setBoatId] = useState(trip.boatId || boats[0]?.id || '')
  const { run, busy, error } = useSubmit()

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const result = await run(() => assignTripBoat(trip.id, boatId))
    if (result) onDone(result)
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <p className="text-sm text-on-surface-variant">
        {t('{route}, departing {time}.', { route: trip.routeName, time: formatDateTime(trip.departureTime) })}
      </p>
      <SelectField
        label="Boat"
        hint={trip.boatId ? 'Seats already sold move to the same seats of the new boat.' : undefined}
        value={boatId}
        onChange={(e) => setBoatId(e.target.value)}
      >
        {boats.map((boat) => (
          <option key={boat.id} value={boat.id}>
            {`${boat.code} · ${boat.name} (${t('{count} seats', { count: boat.totalSeats })})`}
          </option>
        ))}
      </SelectField>
      <FormActions
        error={error}
        busy={busy}
        submitLabel={trip.boatId ? 'Change boat' : 'Assign boat'}
        disabled={!boatId || boatId === trip.boatId}
        onCancel={onCancel}
      />
    </form>
  )
}
