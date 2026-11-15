import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Button, TextField } from '../../../components/ui'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { requestLookupOtp, verifyLookupOtp } from '../services/ticketLookupService'
import type { TicketLookupResult } from '../types'
import TicketLookupResultView from './TicketLookupResultView'

type Step = 'details' | 'code' | 'result'

const OTP_LENGTH = 6
const DEFAULT_RESEND_SECONDS = 60

/** Lookup failures worth telling apart for the traveler; everything else shares one generic message. */
function messageFor(error: unknown, fallbackInvalid: string): string {
  if (error instanceof ApiError) {
    if (error.status === 429) return t('Too many attempts. Please wait a minute and try again.')
    if (error.status === 400) return t('Please check the email and the booking code.')
    if (error.status === 403) return fallbackInvalid
  }
  return t('We couldn’t complete that just now. Please check your connection and try again.')
}

/**
 * Find tickets again without an account: booking code + the purchaser's email, then a one-time code
 * sent to that email. The screen never says whether the booking code exists, and the result is kept
 * in memory only (nothing is stored in the browser).
 */
export default function TicketLookupFlow() {
  const [step, setStep] = useState<Step>('details')
  const [email, setEmail] = useState('')
  const [bookingCode, setBookingCode] = useState('')
  const [otp, setOtp] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [resendIn, setResendIn] = useState(0)
  const [result, setResult] = useState<TicketLookupResult | null>(null)

  // Counts down to the next allowed "send again" so the traveler is not left guessing.
  useEffect(() => {
    if (resendIn <= 0) return
    const timer = window.setTimeout(() => setResendIn((seconds) => seconds - 1), 1000)
    return () => window.clearTimeout(timer)
  }, [resendIn])

  const sendCode = async () => {
    setBusy(true)
    setError(null)
    try {
      const info = await requestLookupOtp(email, bookingCode)
      setResendIn(info.resendAfterSeconds || DEFAULT_RESEND_SECONDS)
      setOtp('')
      setStep('code')
    } catch (e) {
      setError(messageFor(e, ''))
    } finally {
      setBusy(false)
    }
  }

  const submitDetails = (e: FormEvent) => {
    e.preventDefault()
    void sendCode()
  }

  const submitCode = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      setResult(await verifyLookupOtp(email, bookingCode, otp))
      setStep('result')
    } catch (err) {
      setError(messageFor(err, t('The code is wrong or has expired. Request a new code.')))
    } finally {
      setBusy(false)
    }
  }

  const startOver = () => {
    setResult(null)
    setOtp('')
    setError(null)
    setStep('details')
  }

  if (step === 'result' && result) {
    return <TicketLookupResultView result={result} onDone={startOver} />
  }

  return (
    <div className="w-full rounded-2xl border border-outline-variant/40 bg-white p-space-xl shadow-sm">
      {error && (
        <p role="alert" className="mb-space-md rounded-xl border border-coral-glow/30 bg-coral-glow/10 px-4 py-3 text-sm text-deep-river">
          {error}
        </p>
      )}

      {step === 'details' ? (
        <form onSubmit={submitDetails} className="flex flex-col gap-space-md" noValidate>
          <TextField
            label={t('Email used to book')}
            type="email"
            autoComplete="email"
            placeholder={t('name@example.com')}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
          <TextField
            label={t('Booking code')}
            hint={t('Shown in the confirmation email, starting with WB.')}
            placeholder="WB20261114101530ABCDEF"
            autoCapitalize="characters"
            spellCheck={false}
            autoComplete="off"
            value={bookingCode}
            onChange={(e) => setBookingCode(e.target.value.toUpperCase())}
          />
          <Button type="submit" variant="dark" disabled={busy || !email.trim() || !bookingCode.trim()}>
            {busy ? t('Sending…') : t('Send me a code')}
          </Button>
        </form>
      ) : (
        <form onSubmit={submitCode} className="flex flex-col gap-space-md" noValidate>
          <p className="text-body-md text-on-surface-variant">
            {t('If the details match a booking, we sent a 6-digit code to that email. It is valid for a few minutes.')}
          </p>
          <TextField
            label={t('6-digit code')}
            inputMode="numeric"
            autoComplete="one-time-code"
            maxLength={OTP_LENGTH}
            placeholder="123456"
            value={otp}
            onChange={(e) => setOtp(e.target.value.replace(/\D/g, '').slice(0, OTP_LENGTH))}
          />
          <Button type="submit" variant="dark" disabled={busy || otp.length !== OTP_LENGTH}>
            {busy ? t('Checking…') : t('View my tickets')}
          </Button>
          <div className="flex items-center justify-between text-sm">
            <button type="button" className="font-semibold text-teal-flow disabled:text-outline" disabled={busy || resendIn > 0} onClick={() => void sendCode()}>
              {resendIn > 0 ? t('Send again in {n}s').replace('{n}', String(resendIn)) : t('Send a new code')}
            </button>
            <button type="button" className="text-on-surface-variant underline" onClick={startOver}>
              {t('Change details')}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}
