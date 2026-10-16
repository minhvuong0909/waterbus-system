import type { ReactNode } from 'react'
import AssistantWidget from '../features/assistant/components/AssistantWidget'
import { t } from '../i18n'
import Footer from './footer/Footer'
import Header from './header/Header'

interface MainLayoutProps {
  children: ReactNode
}

/** Shared shell: fixed header (h-20), page content, footer. */
export default function MainLayout({ children }: MainLayoutProps) {
  return (
    <div className="flex min-h-screen flex-col bg-surface">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[100] focus:rounded-lg focus:bg-deep-river focus:px-4 focus:py-2 focus:text-on-primary"
      >
        {t('Skip to main content')}
      </a>
      <Header />
      <main id="main-content" className="w-full flex-1 pt-20 print:pt-0">{children}</main>
      <Footer />
      <AssistantWidget />
    </div>
  )
}
