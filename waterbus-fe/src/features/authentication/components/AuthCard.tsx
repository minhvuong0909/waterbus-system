import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ApiError } from '../../../services/apiClient'
import type { SignInPayload } from '../types'
import { landingRouteFor } from '../../../routes/landingRoute'
import { useAuth } from '../hooks/useAuth'
import { signIn } from '../services/authenticationService'
import AuthShowcasePanel from './AuthShowcasePanel'
import SignInForm from './SignInForm'
import { t } from '../../../i18n'

/** Sign-in with an account the operator issued (the backend has no self-registration). */
export default function AuthCard() {
  const navigate = useNavigate()
  const location = useLocation()
  const { startSession } = useAuth()
  const [submitting, setSubmitting] = useState(false)
  // Message from the server (e.g. wrong password), or '' for the generic connection error.
  const [failure, setFailure] = useState<string | null>(null)

  const from = (location.state as { from?: string } | null)?.from

  const submit = async (payload: SignInPayload) => {
    setSubmitting(true)
    setFailure(null)
    try {
      const session = await signIn(payload)
      startSession(session, payload.remember)
      navigate(from ?? landingRouteFor(session.user.roles), { replace: true })
    } catch (error) {
      setSubmitting(false)
      // Only server-sent messages are shown as they are; a status of 0 means no connection.
      setFailure(error instanceof ApiError && error.status !== 0 ? error.message : '')
    }
  }

  return (
    <div className="grid w-full grid-cols-1 overflow-hidden rounded-3xl border border-outline-variant/50 bg-white shadow-sm lg:grid-cols-12">
      <AuthShowcasePanel />

      <div className="flex min-w-0 flex-col justify-center bg-white p-5 sm:p-10 lg:col-span-7 lg:p-12">
        <div className="mx-auto w-full max-w-md">
          <h1 className="text-2xl font-bold tracking-tight text-deep-river">{t('Sign in to your account')}</h1>
          <p className="mb-8 mt-2 text-sm text-on-surface-variant">
            {t('Sign in to manage your bookings and river journeys.')}
          </p>

          {failure !== null && (
            <p
              role="alert"
              className="mb-4 rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-4 py-3 text-sm text-deep-river"
            >
              {failure || t('We couldn’t complete that just now. Please check your connection and try again.')}
            </p>
          )}

          <SignInForm submitting={submitting} onSubmit={submit} />

          <p className="mt-8 flex items-center gap-2 border-t border-surface-container pt-5 text-xs text-on-surface-variant">
            <Icon name="info" className="text-base text-teal-flow" />
            {t('Accounts are issued by Smart Waterbus. You can book tickets without signing in.')}
          </p>
        </div>
      </div>
    </div>
  )
}
