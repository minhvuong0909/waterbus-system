import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '../../../components/ui'
import { SIGN_IN_DEFAULTS } from '../../../mocks/auth'
import type { SignInPayload } from '../authenticationTypes'
import AuthButton from './authButton'
import AuthField from './AuthField'
import { t } from '../../../i18n'

interface SignInFormProps {
  submitting: boolean
  onSubmit: (payload: SignInPayload) => void
  onGoogle: () => void
  onSwitchToRegister: () => void
}

export default function SignInForm({
  submitting,
  onSubmit,
  onGoogle,
  onSwitchToRegister,
}: SignInFormProps) {
  const [email, setEmail] = useState(SIGN_IN_DEFAULTS.email)
  const [password, setPassword] = useState(SIGN_IN_DEFAULTS.password)
  const [remember, setRemember] = useState(true)

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({ email, password, remember })
  }

  return (
    <form className="space-y-4" onSubmit={handleSubmit} noValidate>
      <AuthButton onClick={onGoogle} disabled={submitting} />

      <div className="flex items-center gap-3 text-[11px] font-semibold uppercase tracking-wider text-outline">
        <span className="h-px flex-1 bg-outline-variant/60" />
        {t('Or continue with email')}
        <span className="h-px flex-1 bg-outline-variant/60" />
      </div>

      <AuthField
        id="signin-email"
        label={t('Email address')}
        icon="mail"
        type="email"
        placeholder={t('name@example.com')}
        autoComplete="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
      />
      <AuthField
        id="signin-password"
        label={t('Password')}
        icon="lock"
        type="password"
        revealable
        placeholder={t('Enter your password')}
        autoComplete="current-password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        labelAside={
          <button
            type="button"
            className="text-xs font-semibold text-teal-flow transition-colors hover:text-deep-river hover:underline"
          >
            {t('Forgot password?')}
          </button>
        }
      />

      <label className="flex cursor-pointer select-none items-center gap-2 pt-1">
        <input
          type="checkbox"
          checked={remember}
          onChange={(e) => setRemember(e.target.checked)}
          className="h-4 w-4 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow focus:ring-offset-0"
        />
        <span className="text-xs font-medium text-on-surface-variant">
          {t('Keep me signed in on this device')}
        </span>
      </label>

      <div className="pt-2">
        <button
          type="submit"
          disabled={submitting}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow px-5 py-3 text-sm font-bold tracking-wide text-white shadow-md shadow-teal-flow/20 transition-all hover:bg-[#0f676b] hover:shadow-lg hover:shadow-teal-flow/30 active:scale-[0.99] disabled:opacity-60"
        >
          <span>{submitting ? t('Signing in…') : t('Sign In')}</span>
          <Icon name="arrow_forward" className="text-[18px]" />
        </button>
      </div>

      <p className="pt-2 text-center text-xs text-on-surface-variant">
        {t('Don’t have an account yet?')}
        <button
          type="button"
          onClick={onSwitchToRegister}
          className="ml-1 font-bold text-teal-flow underline hover:text-deep-river"
        >
          {t('Create Account')}
        </button>
      </p>
    </form>
  )
}
