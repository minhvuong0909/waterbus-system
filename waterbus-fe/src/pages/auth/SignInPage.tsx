import AuthCard from '../../features/authentication/components/AuthCard'
import { useDocumentTitle } from '../../hooks'

/** Đăng nhập cho tài khoản Smart Waterbus cấp; khách đặt vé không cần tài khoản. */
export default function SignInPage() {
  useDocumentTitle('Sign in')

  return (
    <div className="flex justify-center bg-mist px-4 py-space-2xl sm:px-8">
      <AuthCard />
    </div>
  )
}
