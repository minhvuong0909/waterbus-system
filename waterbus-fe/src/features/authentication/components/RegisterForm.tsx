import { useState } from 'react'
import type { FormEvent } from 'react'
import { Icon } from '../../../components/ui'
import type { RegisterPayload } from '../authenticationTypes'
import AuthButton from './authButton'
import AuthField from './AuthField'
import { t } from '../../../i18n'

interface RegisterFormProps {
  submitting: boolean
  onSubmit: (payload: RegisterPayload) => void
  onGoogle: () => void
  onSwitchToSignIn: () => void
}

export default function RegisterForm({
  submitting,
  onSubmit,
  onGoogle,
  onSwitchToSignIn,
}: RegisterFormProps) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [agreed, setAgreed] = useState(true)

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({ fullName, email, phone, password })
  }

  return (
    <form className="space-y-3.5" onSubmit={handleSubmit} noValidate>
      <AuthButton onClick={onGoogle} disabled={submitting}>
        {t('Sign up with Google')}
      </AuthButton>

      <div className="flex items-center gap-3 text-[11px] font-semibold uppercase tracking-wider text-outline">
        <span className="h-px flex-1 bg-outline-variant/60" />
        {t('Or register with email')}
        <span className="h-px flex-1 bg-outline-variant/60" />
      </div>

      <AuthField
        id="reg-name"
        label={t('Full name')}
        icon="person"
        placeholder={t('e.g. Nguyen Van An')}
        autoComplete="name"
        value={fullName}
        onChange={(e) => setFullName(e.target.value)}
      />
      <AuthField
        id="reg-email"
        label={t('Email address')}
        icon="mail"
        type="email"
        placeholder={t('passenger@example.com')}
        autoComplete="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
      />
      <AuthField
        id="reg-phone"
        label={t('Phone number')}
        icon="call"
        type="tel"
        placeholder="+84 90 123 4567"
        autoComplete="tel"
        value={phone}
        onChange={(e) => setPhone(e.target.value)}
      />
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <AuthField
          id="reg-password"
          label={t('Password')}
          type="password"
          placeholder={t('Min. 8 characters')}
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <AuthField
          id="reg-confirm"
          label={t('Confirm password')}
          type="password"
          placeholder={t('Repeat password')}
          autoComplete="new-password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
        />
      </div>

      <label className="flex cursor-pointer select-none items-start gap-2 pt-1">
        <input
          type="checkbox"
          checked={agreed}
          onChange={(e) => setAgreed(e.target.checked)}
          className="mt-0.5 h-4 w-4 rounded border-outline-variant text-teal-flow accent-teal-flow focus:ring-teal-flow"
        />
        <span className="text-[11px] leading-snug text-on-surface-variant">
          {t('I agree to the')}{' '}
          <span className="font-medium text-teal-flow hover:underline">
            {t('Terms of Carriage')}
          </span>{' '}
          {t('and acknowledge the')}{' '}
          <span className="font-medium text-teal-flow hover:underline">
            {t('Waterway Safety & Privacy Notice')}
          </span>
          .
        </span>
      </label>

      <div className="pt-2">
        <button
          type="submit"
          disabled={submitting}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-teal-flow px-5 py-3 text-sm font-bold tracking-wide text-white shadow-md shadow-teal-flow/20 transition-all hover:bg-[#0f676b] hover:shadow-lg hover:shadow-teal-flow/30 active:scale-[0.99] disabled:opacity-60"
        >
          <span>{submitting ? t('Creating account…') : t('Create Account')}</span>
          <Icon name="arrow_forward" className="text-[18px]" />
        </button>
      </div>

      <p className="pt-2 text-center text-xs text-on-surface-variant">
        {t('Already registered with Smart Waterbus?')}
        <button
          type="button"
          onClick={onSwitchToSignIn}
          className="ml-1 font-bold text-teal-flow underline hover:text-deep-river"
        >
          {t('Sign In')}
        </button>
      </p>
    </form>
  )
}
