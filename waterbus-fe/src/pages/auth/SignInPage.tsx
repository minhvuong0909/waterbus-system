import { Breadcrumb } from '../../components/ui'
import AuthCard from '../../features/authentication/components/AuthCard'
import GuestLookupStrip from '../../features/authentication/components/GuestLookupStrip'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { Reveal } from '../../components/motion'

export default function SignInPage() {
  useDocumentTitle('Account Access')

  return (
    <Reveal className="bg-mist pb-4">
      <div className="mx-auto w-full max-w-6xl px-4 pb-3 pt-5 sm:px-8">
        <Breadcrumb
          items={[{ label: 'Home', to: ROUTES.home }, { label: 'Passenger Account Access' }]}
        />
      </div>
      <div
        data-reveal
        className="mx-auto flex w-full max-w-6xl items-center justify-center px-4 py-4 sm:px-8"
      >
        <AuthCard />
      </div>
      <div className="mx-auto w-full max-w-6xl px-4 py-4 sm:px-8">
        <GuestLookupStrip />
      </div>
    </Reveal>
  )
}
