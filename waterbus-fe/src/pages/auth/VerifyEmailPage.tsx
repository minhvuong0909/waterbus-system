import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Button, Icon } from '../../components/ui'
import AuthField from '../../features/authentication/components/AuthField'
import ResendVerificationForm from '../../features/authentication/components/ResendVerificationForm'
import { confirmEmail } from '../../features/authentication/services/registrationService'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { ApiError } from '../../services/apiClient'

/** Keeps the email link in memory only. Query links sent before activation was updated still work. */
export default function VerifyEmailPage() {
  useDocumentTitle('Verify email')
  const location = useLocation()
  const navigate = useNavigate()
  const [link, setLink] = useState(() => {
    const params = new URLSearchParams(location.hash ? location.hash.slice(1) : location.search)
    const uid = params.get('uid')
    const token = params.get('token')
    return uid && token ? { uid, token } : null
  })
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [outcome, setOutcome] = useState<'confirmed' | 'failed' | null>(null)

  useEffect(() => {
    if (location.search || location.hash) navigate(location.pathname, { replace: true })
  }, [location.hash, location.pathname, location.search, navigate])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!link || busy) return
    if (password !== confirm) {
      setError(t('The two passwords do not match.'))
      return
    }
    setError(null)
    setBusy(true)
    try {
      await confirmEmail(link.uid, link.token, password)
      setOutcome('confirmed')
      setLink(null)
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        const passwordError = err.fieldErrors.password?.[0] ?? err.fieldErrors.Password?.[0]
        if (passwordError) {
          setError(passwordError)
        } else {
          setOutcome('failed')
          setLink(null)
        }
      } else {
        setError(err instanceof ApiError && err.status === 429
          ? t('Too many attempts. Please wait a minute and try again.')
          : t('We couldn’t complete that just now. Please check your connection and try again.'))
      }
    } finally {
      setPassword('')
      setConfirm('')
      setBusy(false)
    }
  }

  return (
    <div className="flex justify-center bg-mist px-4 py-space-2xl sm:px-8">
      <div className="w-full max-w-md space-y-space-md rounded-3xl border border-outline-variant/50 bg-white p-6 text-center shadow-sm sm:p-10">
        {link && (
          <>
            <h1 className="font-display text-headline-md text-deep-river">{t('Set your password')}</h1>
            <p className="text-sm text-on-surface-variant">
              {t('Password must be at least 8 characters, with upper case, lower case and a digit.')}
            </p>
            <form className="space-y-4 text-left" onSubmit={submit}>
              <AuthField id="activate-password" label={t('Password')} icon="lock" type="password" revealable
                autoComplete="new-password" required maxLength={100} value={password} onChange={(e) => setPassword(e.target.value)} />
              <AuthField id="activate-confirm" label={t('Confirm password')} icon="lock" type="password" revealable
                autoComplete="new-password" required maxLength={100} value={confirm} onChange={(e) => setConfirm(e.target.value)} />
              {error && <p role="alert" className="text-sm text-coral-glow">{error}</p>}
              <Button type="submit" variant="dark" className="w-full" disabled={busy}>
                {busy ? t('Activating account…') : t('Activate account')}
              </Button>
            </form>
            <details className="text-left">
              <summary className="cursor-pointer text-sm font-semibold text-teal-flow">{t('Need a new verification link?')}</summary>
              <div className="mt-space-sm"><ResendVerificationForm /></div>
            </details>
          </>
        )}

        {outcome === 'confirmed' && (
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

        {!link && outcome !== 'confirmed' && (
          <>
            <h1 className="font-display text-headline-md text-deep-river">
              {outcome === 'failed' ? t('This link is invalid or has expired') : t('Verify your email')}
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
