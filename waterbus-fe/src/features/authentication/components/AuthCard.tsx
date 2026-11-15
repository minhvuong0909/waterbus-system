import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ROUTES } from '../../../routes/routes'
import { Icon } from '../../../components/ui'
import { ApiError } from '../../../services/apiClient'
import type { SignInPayload } from '../types'
import { useAuth } from '../hooks/useAuth'
import { signIn } from '../services/authenticationService'
import SignInForm from './SignInForm'
import { t } from '../../../i18n'

/** Sign-in with an account the operator issued (the backend has no self-registration). */
export default function AuthCard() {
  const { startSession } = useAuth()
  const [submitting, setSubmitting] = useState(false)
  // Message from the server (e.g. wrong password), or '' for the generic connection error.
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async (payload: SignInPayload) => {
    setSubmitting(true)
    setFailure(null)
    try {
      const session = await signIn(payload)
      // Trang đăng nhập tự chuyển đi khi đã có phiên (về trang đang định vào hoặc khu làm việc).
      startSession(session, payload.remember)
    } catch (error) {
      setSubmitting(false)
      // Only server-sent messages are shown as they are; a status of 0 means no connection.
      setFailure(error instanceof ApiError && error.status !== 0 ? error.message : '')
    }
  }

  return (
    <div>
      <h1 className="font-display text-3xl font-bold tracking-tight text-deep-river">{t('Welcome back')}</h1>
      <p className="mb-8 mt-2 text-sm text-on-surface-variant">{t('Sign in to continue to Smart Waterbus.')}</p>

      {failure !== null && (
        <p
          role="alert"
          className="mb-4 rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-4 py-3 text-sm text-deep-river"
        >
          {failure ||
            t('We couldn’t complete that just now. Please check your connection and try again.')}
        </p>
      )}

      <SignInForm submitting={submitting} onSubmit={submit} />

      <p className="mt-6 text-center text-sm text-on-surface-variant">
        {t('New here?')}{' '}
        <Link to={ROUTES.signUp} className="font-bold text-teal-flow">
          {t('Create account')}
        </Link>
        {' · '}
        <Link to={ROUTES.verifyEmail} className="font-bold text-teal-flow">
          {t('Resend verification email')}
        </Link>
      </p>

      <div className="mt-8 flex items-center justify-between gap-3 rounded-2xl bg-mist px-4 py-3">
        <p className="flex items-center gap-2 text-xs text-on-surface-variant">
          <Icon name="info" className="text-base text-teal-flow" />
          {t('Travellers do not need an account to book tickets.')}
        </p>
        <Link to={ROUTES.home} className="shrink-0 text-xs font-bold text-teal-flow hover:underline">
          {t('Book a ticket')}
        </Link>
      </div>
    </div>
  )
}
