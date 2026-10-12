import { Suspense } from 'react'
import type { ComponentType, ReactNode } from 'react'
import { PageLoader } from '../components/ui'
import MainLayout from '../layouts/MainLayout'

interface RouteWithLayoutProps {
  children: ReactNode
  layout?: ComponentType<{ children: ReactNode }>
}

/** Renders the layout immediately and suspends only the page content while its chunk loads. */
export default function RouteWithLayout({
  children,
  layout: Layout = MainLayout,
}: RouteWithLayoutProps) {
  return (
    <Layout>
      <Suspense fallback={<PageLoader />}>{children}</Suspense>
    </Layout>
  )
}
