import { useState } from 'react'
import type { FormEvent } from 'react'
import { Button, ErrorState, PageLoader, TextField } from '../../../components/ui'
import { useFetch } from '../../../hooks'
import { t } from '../../../i18n'
import { ApiError } from '../../../services/apiClient'
import { getProfile, updateProfile } from '../services/profileService'
import type { Profile } from '../types'

function Editor({ profile }: { profile: Profile }) {
  const [fullName, setFullName] = useState(profile.fullName)
  const [phone, setPhone] = useState(profile.phoneNumber ?? '')
  const [busy, setBusy] = useState(false)
  const [saved, setSaved] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setSaved(false)
    setError(null)
    try {
      await updateProfile(fullName, phone)
      setSaved(true)
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 400
          ? t('Please check the details you entered.')
          : t('We couldn’t complete that just now. Please check your connection and try again.'),
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-space-md rounded-2xl border border-outline-variant/40 bg-white p-space-xl shadow-sm" noValidate>
      <TextField label="Email address" value={profile.email} readOnly disabled hint={profile.emailConfirmed ? 'Verified' : 'Not verified'} />
      <TextField label="Full name" value={fullName} onChange={(e) => setFullName(e.target.value)} autoComplete="name" />
      <TextField label="Phone number (optional)" type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} autoComplete="tel" />
      {error && <p role="alert" className="text-sm text-coral-glow">{error}</p>}
      {saved && <p role="status" className="text-sm text-on-surface-variant">{t('Saved.')}</p>}
      <Button type="submit" variant="dark" disabled={busy || !fullName.trim()}>
        {busy ? t('Saving…') : t('Save changes')}
      </Button>
    </form>
  )
}

/** The customer's own details. The id comes from the sign-in, never from the URL. */
export default function ProfileForm() {
  const profile = useFetch(getProfile)
  if (profile.error) return <ErrorState onRetry={profile.retry} />
  if (profile.loading || !profile.data) return <PageLoader />
  return <Editor profile={profile.data} />
}
