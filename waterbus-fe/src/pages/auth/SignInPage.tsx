import { Breadcrumb } from '../../components/ui'
import AuthCard from '../../features/authentication/components/AuthCard'
import GuestLookupStrip from '../../features/authentication/components/GuestLookupStrip'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'

export default function SignInPage() {
  useDocumentTitle('Account Access')

  return (
    <div className="bg-mist">
      <div className="mx-auto w-full max-w-[1440px] px-8 pb-3 pt-5">
        <Breadcrumb
          items={[{ label: 'Home', to: ROUTES.home }, { label: 'Passenger Account Access' }]}
        />
      </div>
      <div className="mx-auto flex w-full max-w-[1440px] items-center justify-center px-8 py-4">
        <AuthCard />
      </div>
      <div className="mx-auto w-full max-w-[1440px] px-8 py-4">
        <GuestLookupStrip />
      </div>
    </div>
  )
}
