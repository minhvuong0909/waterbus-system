import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '../../../components/ui'
import type { SignInPayload } from '../types'
import AuthField from './AuthField'
import { t } from '../../../i18n'
import { isEmail } from '../../../utils/inputRules'

interface SignInFormProps {
  submitting: boolean
  onSubmit: (payload: SignInPayload) => void
}

export default function SignInForm({ submitting, onSubmit }: SignInFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [remember, setRemember] = useState(true)

  const [showErrors, setShowErrors] = useState(false)
  const errors = {
    email: isEmail(email) ? undefined : t('Please enter a valid email address.'),
    password: password ? undefined : t('Please enter your password.'),
  }

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (errors.email || errors.password) {
      setShowErrors(true)
      return
    }
    onSubmit({ email: email.trim(), password, remember })
  }

  return (
    <form className="space-y-4" onSubmit={handleSubmit} noValidate>
      <AuthField
        id="signin-email"
        required
        label={t('Email address')}
        icon="mail"
        type="email"
        placeholder={t('name@example.com')}
        autoComplete="email"
        maxLength={150}
        error={showErrors ? errors.email : undefined}
        value={email}
        onChange={(e) => setEmail(e.target.value)}
      />
      <AuthField
        id="signin-password"
        required
        label={t('Password')}
        icon="lock"
        type="password"
        revealable
        placeholder={t('Enter your password')}
        autoComplete="current-password"
        maxLength={100}
        error={showErrors ? errors.password : undefined}
        value={password}
        onChange={(e) => setPassword(e.target.value)}
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
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow px-5 py-3.5 text-sm font-bold tracking-wide text-white shadow-md shadow-teal-flow/25 transition-all hover:bg-deep-river hover:shadow-lg hover:shadow-teal-flow/30 active:scale-[0.99] disabled:opacity-60"
        >
          <span>{submitting ? t('Signing in…') : t('Sign In')}</span>
          <Icon name="arrow_forward" className="text-lg" />
        </button>
      </div>

    </form>
  )
}
