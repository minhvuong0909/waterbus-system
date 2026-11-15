import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { resendVerification } from '../services/registrationService'

const COOLDOWN_SECONDS = 60

/** Asks for the verification email again. The answer never says whether the email has an account. */
export default function ResendVerificationForm({ initialEmail = '' }: { initialEmail?: string }) {
  const [email, setEmail] = useState(initialEmail)
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [wait, setWait] = useState(0)

  useEffect(() => {
    if (wait <= 0) return
    const timer = window.setTimeout(() => setWait((seconds) => seconds - 1), 1000)
    return () => window.clearTimeout(timer)
  }, [wait])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await resendVerification(email)
      setSent(true)
      setWait(COOLDOWN_SECONDS)
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 429
          ? t('Too many attempts. Please wait a minute and try again.')
          : err instanceof ApiError && err.status === 400
            ? t('Please enter a valid email address.')
            : t('We couldn’t complete that just now. Please check your connection and try again.'),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-space-sm" noValidate>
      <TextField
        label="Email address"
        type="email"
        autoComplete="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
      />
      {error && (
        <p role="alert" className="text-sm text-coral-glow">
          {error}
        </p>
      )}
      {sent && (
        <p role="status" className="text-sm text-on-surface-variant">
          {t('If your account can be activated, check your inbox and spam folder for a link to set your password.')}
        </p>
      )}
      <Button type="submit" variant="dark" disabled={busy || wait > 0 || !email.trim()}>
        {wait > 0 ? t('Send again in {n}s').replace('{n}', String(wait)) : t('Send verification email')}
      </Button>
    </form>
  )
}
