import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { ErrorState, Icon } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { ApiError } from '../../../services/apiClient'
import type { AuthMode, AuthSession, RegisterPayload, SignInPayload } from '../authenticationTypes'
import { landingRouteFor } from '../../admin/navigation'
import { useAuth } from '../hook/authenticationHook'
import { getAuthShowcase, register, signIn, signInWithGoogle } from '../services/authenticationApi'
import AuthShowcasePanel from './AuthShowcasePanel'
import RegisterForm from './RegisterForm'
import SignInForm from './SignInForm'
import { t } from '../../../i18n'

const COPY: Record<AuthMode, { title: string; subtitle: string }> = {
  signin: {
    title: 'Sign in to your account',
    subtitle: 'Sign in to manage your bookings and river journeys.',
  },
  register: {
    title: 'Create your account',
    subtitle: 'Register to save trips, tickets and passenger details.',
  },
}

export default function AuthCard() {
  const navigate = useNavigate()
  const location = useLocation()
  const { startSession } = useAuth()
  const [mode, setMode] = useState<AuthMode>('signin')
  const [submitting, setSubmitting] = useState(false)
  // Message from the server (e.g. wrong password), or '' for the generic connection error.
  const [failure, setFailure] = useState<string | null>(null)
  const showcase = useFetch(getAuthShowcase)

  const from = (location.state as { from?: string } | null)?.from

  const finish = async (action: Promise<AuthSession>, remember = true) => {
    setSubmitting(true)
    setFailure(null)
    try {
      const session = await action
      startSession(session, remember)
      navigate(from ?? landingRouteFor(session.user.roles), { replace: true })
    } catch (error) {
      setSubmitting(false)
      // Only server-sent messages are shown as they are; a status of 0 means no connection.
      setFailure(error instanceof ApiError && error.status !== 0 ? error.message : '')
    }
  }

  const tabClass = (active: boolean) =>
    `flex flex-1 items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-xs transition-all ${
      active
        ? 'bg-white font-bold text-deep-river shadow-sm'
        : 'font-semibold text-on-surface-variant hover:text-deep-river'
    }`

  return (
    <div className="grid w-full grid-cols-1 overflow-hidden rounded-[24px] border border-outline-variant/50 bg-white shadow-[0_12px_40px_-24px_rgba(13,37,56,0.25)] lg:min-h-[600px] lg:grid-cols-12">
      {showcase.data ? (
        <AuthShowcasePanel showcase={showcase.data} />
      ) : showcase.error ? (
        <div className="hidden items-center bg-deep-river p-8 lg:col-span-5 lg:flex">
          <div className="w-full">
            <ErrorState compact onRetry={showcase.retry} />
          </div>
        </div>
      ) : (
        <div className="hidden bg-deep-river lg:col-span-5 lg:block" />
      )}

      <div className="flex min-w-0 flex-col justify-between bg-white p-5 sm:p-10 lg:col-span-7 lg:p-12">
        <div className="mx-auto w-full max-w-md">
          <div className="mb-7">
            <div className="mb-2 flex flex-wrap items-center justify-between gap-3">
              <h1 className="text-2xl font-bold leading-snug tracking-tight text-deep-river">
                {t(COPY[mode].title)}
              </h1>
              <span className="shrink-0 rounded-full bg-teal-flow/10 px-2.5 py-1 text-xs font-semibold text-teal-flow">
                {t('Passenger Portal')}
              </span>
            </div>
            <p className="text-sm text-on-surface-variant">{t(COPY[mode].subtitle)}</p>
          </div>

          <div
            role="tablist"
            className="mb-8 flex items-center rounded-xl border border-outline-variant/60 bg-surface-container p-1"
          >
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'signin'}
              onClick={() => setMode('signin')}
              className={tabClass(mode === 'signin')}
            >
              <Icon
                name="login"
                className={`text-[16px] ${mode === 'signin' ? 'text-teal-flow' : 'text-outline'}`}
              />
              <span>{t('Sign In')}</span>
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'register'}
              onClick={() => setMode('register')}
              className={tabClass(mode === 'register')}
            >
              <Icon
                name="person_add"
                className={`text-[16px] ${mode === 'register' ? 'text-teal-flow' : 'text-outline'}`}
              />
              <span>{t('Create Account')}</span>
            </button>
          </div>

          {failure !== null && (
            <p
              role="alert"
              className="mb-4 rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-4 py-3 text-sm text-deep-river"
            >
              {failure ||
                t(
                  'We couldn’t complete that just now. Please check your connection and try again.',
                )}
            </p>
          )}

          {mode === 'signin' ? (
            <SignInForm
              submitting={submitting}
              onSubmit={(p: SignInPayload) => finish(signIn(p), p.remember)}
              onGoogle={() => finish(signInWithGoogle())}
              onSwitchToRegister={() => setMode('register')}
            />
          ) : (
            <RegisterForm
              submitting={submitting}
              onSubmit={(p: RegisterPayload) => finish(register(p))}
              onGoogle={() => finish(signInWithGoogle())}
              onSwitchToSignIn={() => setMode('signin')}
            />
          )}
        </div>

        <div className="mt-8 flex flex-wrap items-center justify-between gap-3 border-t border-surface-container pt-5 text-xs text-on-surface-variant">
          <div className="flex items-center gap-2">
            <Icon name="shield" className="text-[16px] text-teal-flow" />
            <span>{t('Secure Passenger Account')}</span>
          </div>
          <span>{t('Access your saved trips and tickets')}</span>
        </div>
      </div>
    </div>
  )
}
