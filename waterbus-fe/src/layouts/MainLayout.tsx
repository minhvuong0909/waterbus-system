import type { ReactNode } from 'react'
import AssistantWidget from '../features/assistant/components/AssistantWidget'
import Footer from './footer/Footer'
import Header from './header/Header'

interface MainLayoutProps {
  children: ReactNode
}

/** Shared shell: fixed header (h-20), page content, footer. */
export default function MainLayout({ children }: MainLayoutProps) {
  return (
    <div className="flex min-h-screen flex-col bg-surface">
      <Header />
      <main className="w-full flex-1 pt-20 print:pt-0">{children}</main>
      <Footer />
      <AssistantWidget />
    </div>
  )
}
