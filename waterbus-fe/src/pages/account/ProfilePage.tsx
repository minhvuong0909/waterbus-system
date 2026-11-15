import { Breadcrumb } from '../../components/ui'
import ProfileForm from '../../features/account/components/ProfileForm'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'

/** Customer profile (Passenger only). */
export default function ProfilePage() {
  useDocumentTitle('My details')
  return (
    <div className="mx-auto w-full max-w-lg px-margin pb-space-3xl pt-space-md">
      <Breadcrumb items={[{ label: 'Home', to: ROUTES.home }, { label: 'My Tickets', to: ROUTES.myTickets }, { label: 'My details' }]} />
      <h1 className="mb-space-lg mt-space-sm font-display text-4xl font-bold tracking-tight text-deep-river">{t('My details')}</h1>
      <ProfileForm />
    </div>
  )
}
