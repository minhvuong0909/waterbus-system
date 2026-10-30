import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { holdCleared } from '../bookingSlice'
import { useHoldCountdown } from '../hooks/useHoldCountdown'
import { t } from '../../../i18n'

const LOW_SECONDS = 60

function formatClock(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

/**
 * Countdown of the 10 minute seat hold. When it reaches zero the seats are released, so the
 * traveler is sent back to choose again. Shows `fallback` while no booking exists yet.
 */
export default function HoldCountdown({ fallback = null }: { fallback?: ReactNode }) {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { hold, remainingSeconds, expired } = useHoldCountdown()

  if (!hold) return <>{fallback}</>

  if (expired) {
    const chooseAgain = () => {
      dispatch(holdCleared())
      navigate(ROUTES.seatSelection)
    }
    return (
      <div
        role="alert"
        className="flex flex-wrap items-center gap-space-sm rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-space-md py-2 text-xs text-deep-river"
      >
        <Icon name="timer_off" className="text-base text-coral-glow" />
        {t('Your seat hold has expired and the seat was released.')}
        <button
          type="button"
          onClick={chooseAgain}
          className="rounded-lg bg-white px-3 py-1 font-semibold text-teal-flow shadow-sm hover:bg-surface-container-low"
        >
          {t('Choose seats again')}
        </button>
      </div>
    )
  }

  const low = remainingSeconds <= LOW_SECONDS
  return (
    <span
      role="timer"
      aria-live="off"
      className={`inline-flex items-center gap-2 rounded-xl px-space-md py-2 text-xs ${
        low ? 'bg-coral-glow/15 text-coral-glow' : 'bg-surface-container text-on-surface-variant'
      }`}
    >
      <Icon name="timer" className="text-base" />
      {t('Your seat is held for')}
      <strong className="font-mono text-sm text-deep-river">{formatClock(remainingSeconds)}</strong>
    </span>
  )
}
