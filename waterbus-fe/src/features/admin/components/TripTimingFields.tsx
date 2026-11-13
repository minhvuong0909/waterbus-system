import { TextField } from '../../../components/ui'
import type { TripTiming } from '../utils/tripTiming'

/** When sales close and check-in opens for the trips being created; both may stay empty. */
export default function TripTimingFields({
  timing,
  errors,
  onChange,
}: {
  timing: TripTiming
  errors: Partial<Record<keyof TripTiming, string>>
  onChange: (timing: TripTiming) => void
}) {
  const set = (key: keyof TripTiming) => (e: { target: { value: string } }) =>
    onChange({ ...timing, [key]: e.target.value })

  return (
    <div className="grid grid-cols-2 gap-3">
      <TextField
        label="Sales close (minutes before)"
        hint="Empty: sales close at departure."
        inputMode="numeric"
        value={timing.salesClose}
        onChange={set('salesClose')}
        error={errors.salesClose}
      />
      <TextField
        label="Check-in opens (minutes before)"
        hint="Empty: set later on the Check-in page."
        inputMode="numeric"
        value={timing.checkInOpen}
        onChange={set('checkInOpen')}
        error={errors.checkInOpen}
      />
    </div>
  )
}
