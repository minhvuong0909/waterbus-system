import { Suspense } from 'react'
import type { ComponentType, ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import { PageLoader } from '../components/ui'
import MainLayout from '../layouts/MainLayout'

interface RouteWithLayoutProps {
  children: ReactNode
  layout?: ComponentType<{ children: ReactNode }>
}

/** Renders the layout immediately and suspends only the page content while its chunk loads; the page fades in. */
export default function RouteWithLayout({
  children,
  layout: Layout = MainLayout,
}: RouteWithLayoutProps) {
  const { pathname } = useLocation()
  return (
    <Layout>
      {/* Trang mới hiện dần (chỉ độ mờ, không dịch chuyển để không ảnh hưởng nút/hộp cố định). */}
      <div key={pathname} className="page-enter">
        <Suspense fallback={<PageLoader />}>{children}</Suspense>
      </div>
    </Layout>
  )
}
