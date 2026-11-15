import { Navigate, useLocation } from 'react-router-dom'
import AuthLayout from '../../features/authentication/components/AuthLayout'
import AuthCard from '../../features/authentication/components/AuthCard'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { useDocumentTitle } from '../../hooks'
import { landingRouteFor } from '../../routes/landingRoute'

/** Đăng nhập cho tài khoản Smart Waterbus cấp; khách đặt vé không cần tài khoản. */
export default function SignInPage() {
  useDocumentTitle('Sign in')
  const { user } = useAuth()
  const from = (useLocation().state as { from?: string } | null)?.from

  // Đã đăng nhập (vừa xong hoặc từ trước): về trang đang định vào, không thì vào khu làm việc.
  if (user) return <Navigate to={from ?? landingRouteFor(user.roles)} replace />

  return (
    <AuthLayout>
      <AuthCard />
    </AuthLayout>
  )
}
