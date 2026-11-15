import { useState } from 'react'
import { Button, Icon } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { getLinkableOrderCount, linkOrdersByEmail } from '../services/orderAccessService'

/** Each call links at most this many orders; the backend says how many remain, so this loop is bounded too. */
const MAX_ROUNDS = 10

/**
 * Offers to add the orders bought as a guest with the account's verified email (BR-ID-04, linked by a
 * verified email). Shows nothing when there are none. Nothing is linked until the traveler presses the button.
 */
export default function LinkOrdersCard({ onLinked }: { onLinked: () => void }) {
  const linkable = useFetch(getLinkableOrderCount)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const count = linkable.data?.count ?? 0
  if (linkable.loading || linkable.error || count === 0) return null

  const link = async () => {
    setBusy(true)
    setError(null)
    try {
      for (let round = 0; round < MAX_ROUNDS; round++) {
        const result = await linkOrdersByEmail()
        if (result.remaining === 0) break
      }
      linkable.retry()
      onLinked()
    } catch (e) {
      setError(
        e instanceof ApiError && e.status === 403
          ? t('Confirm your email first to add your past orders.')
          : t('We couldn’t complete that just now. Please check your connection and try again.'),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mt-space-lg flex flex-col gap-space-sm rounded-2xl border border-teal-flow/30 bg-teal-flow/5 p-space-lg sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-start gap-space-sm">
        <Icon name="link" className="mt-0.5 text-2xl text-teal-flow" />
        <div>
          <p className="font-semibold text-deep-river">
            {t('{n} past orders were bought with your email').replace('{n}', String(count))}
          </p>
          <p className="text-sm text-on-surface-variant">
            {t('Add them to your account to see the tickets and payments here.')}
          </p>
          {error && (
            <p role="alert" className="mt-1 text-sm text-coral-glow">
              {error}
            </p>
          )}
        </div>
      </div>
      <Button variant="dark" onClick={() => void link()} disabled={busy}>
        {busy ? t('Adding…') : t('Add to my account')}
      </Button>
    </div>
  )
}
