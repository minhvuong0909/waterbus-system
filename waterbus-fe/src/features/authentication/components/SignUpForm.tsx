import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { ROUTES } from '../../../routes/routes'
import { register } from '../services/registrationService'
import type { RegisterPayload } from '../types'
import AuthField from './AuthField'
import ResendVerificationForm from './ResendVerificationForm'

/** First message the backend gave for a field, else a generic one. */
function messageFor(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) return t('Too many attempts. Please wait a minute and try again.')
    if (error.status === 400) {
      const first = Object.values(error.fieldErrors).flat()[0]
      return first || t('Please check the details you entered.')
    }
  }
  return t('We couldn’t complete that just now. Please check your connection and try again.')
}

/**
 * Creates a customer account. The result screen is the same whether or not the email already has an
 * account, and tells the visitor to confirm the email before signing in.
 */
export default function SignUpForm() {
  const [form, setForm] = useState<RegisterPayload>({ fullName: '', email: '', phoneNumber: '' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  const set = (key: keyof RegisterPayload) => (e: { target: { value: string } }) =>
    setForm((previous) => ({ ...previous, [key]: e.target.value }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await register(form)
      setDone(true)
    } catch (err) {
      setError(messageFor(err))
    } finally {
      setBusy(false)
    }
  }

  if (done) {
    return (
      <div className="space-y-space-md text-center">
        <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-secondary-container text-on-secondary-container">
          <Icon name="mark_email_read" className="text-3xl" />
        </span>
        <h2 className="font-display text-headline-md text-deep-river">{t('Check your email')}</h2>
        <p className="text-body-md text-on-surface-variant">
          {t('If your account can be activated, check your inbox and spam folder for a link to set your password.')}
        </p>
        <div className="text-left">
          <ResendVerificationForm initialEmail={form.email} />
        </div>
        <Link to={ROUTES.signIn} className="inline-block text-sm font-bold text-teal-flow">
          {t('Go to sign in')}
        </Link>
      </div>
    )
  }

  return (
    <form className="space-y-4" onSubmit={submit} noValidate>
      {error && (
        <p role="alert" className="rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-4 py-3 text-sm text-deep-river">
          {error}
        </p>
      )}
      <AuthField id="signup-name" label={t('Full name')} icon="person" autoComplete="name" value={form.fullName} onChange={set('fullName')} />
      <AuthField id="signup-email" label={t('Email address')} icon="mail" type="email" autoComplete="email" placeholder={t('name@example.com')} value={form.email} onChange={set('email')} />
      <AuthField id="signup-phone" label={t('Phone number (optional)')} icon="call" type="tel" autoComplete="tel" value={form.phoneNumber} onChange={set('phoneNumber')} />
      <p className="text-xs text-on-surface-variant">
        {t('Use the email you booked tickets with, and your past tickets will appear in your account after you confirm it.')}
      </p>
      <button
        type="submit"
        disabled={busy || !form.fullName.trim() || !form.email.trim()}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow px-5 py-3 text-sm font-bold tracking-wide text-white shadow-md shadow-teal-flow/20 transition-all hover:bg-deep-river disabled:opacity-60"
      >
        {busy ? t('Creating account…') : t('Create account')}
      </button>
    </form>
  )
}
