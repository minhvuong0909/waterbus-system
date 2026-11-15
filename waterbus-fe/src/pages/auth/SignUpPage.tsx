import { Link, Navigate } from 'react-router-dom'
import AuthLayout from '../../features/authentication/components/AuthLayout'
import SignUpForm from '../../features/authentication/components/SignUpForm'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { landingRouteFor } from '../../routes/landingRoute'
import { ROUTES } from '../../routes/routes'

/** A guest creates a customer account to keep and review their tickets. Guests can still book without one. */
export default function SignUpPage() {
  useDocumentTitle('Create account')
  const { user } = useAuth()
  if (user) return <Navigate to={landingRouteFor(user.roles)} replace />

  return (
    <AuthLayout>
      <h1 className="font-display text-3xl font-bold tracking-tight text-deep-river">{t('Create your account')}</h1>
      <p className="mb-8 mt-2 text-sm text-on-surface-variant">
        {t('See all your tickets, payments and details in one place. You can still book without an account.')}
      </p>
      <SignUpForm />
      <p className="mt-8 border-t border-surface-container pt-5 text-sm text-on-surface-variant">
        {t('Already have an account?')}{' '}
        <Link to={ROUTES.signIn} className="font-bold text-teal-flow">
          {t('Sign in')}
        </Link>
      </p>
    </AuthLayout>
  )
}
