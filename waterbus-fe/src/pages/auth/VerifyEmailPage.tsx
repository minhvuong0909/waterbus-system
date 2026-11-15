import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Icon } from '../../components/ui'
import ResendVerificationForm from '../../features/authentication/components/ResendVerificationForm'
import { confirmEmail } from '../../features/authentication/services/registrationService'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'

type State = 'confirming' | 'confirmed' | 'failed' | 'resend'

/**
 * Landing page of the link in the verification email (`?uid=&token=`). Without them it only offers to
 * send the email again. The token is sent to the backend once and never kept in the browser.
 */
export default function VerifyEmailPage() {
  useDocumentTitle('Verify email')
  const [params] = useSearchParams()
  const uid = params.get('uid')
  const token = params.get('token')
  const [state, setState] = useState<State>(uid && token ? 'confirming' : 'resend')
  const started = useRef(false)

  useEffect(() => {
    if (!uid || !token || started.current) return // StrictMode runs effects twice
    started.current = true
    confirmEmail(uid, token)
      .then(() => setState('confirmed'))
      .catch(() => setState('failed'))
  }, [uid, token])

  return (
    <div className="flex justify-center bg-mist px-4 py-space-2xl sm:px-8">
      <div className="w-full max-w-md space-y-space-md rounded-3xl border border-outline-variant/50 bg-white p-6 text-center shadow-sm sm:p-10">
        {state === 'confirming' && <p className="text-sm text-on-surface-variant">{t('Confirming your email…')}</p>}

        {state === 'confirmed' && (
          <>
            <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-secondary-container text-on-secondary-container">
              <Icon name="verified" className="text-3xl" />
            </span>
            <h1 className="font-display text-headline-md text-deep-river">{t('Email confirmed')}</h1>
            <p className="text-body-md text-on-surface-variant">
              {t('Your account is active. Sign in to see your tickets.')}
            </p>
            <Link to={ROUTES.signIn} className="inline-block rounded-xl bg-teal-flow px-6 py-3 text-sm font-bold text-white">
              {t('Sign in')}
            </Link>
          </>
        )}

        {(state === 'failed' || state === 'resend') && (
          <>
            <h1 className="font-display text-headline-md text-deep-river">
              {state === 'failed' ? t('This link is invalid or has expired') : t('Verify your email')}
            </h1>
            <p className="text-body-md text-on-surface-variant">
              {t('Enter your email and we will send you a new verification link.')}
            </p>
            <div className="text-left">
              <ResendVerificationForm />
            </div>
          </>
        )}
      </div>
    </div>
  )
}
