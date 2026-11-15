import type { ReactNode } from 'react'
import '../../features/captain/captain.css'

/** Không sidebar: ưu tiên bản đồ và thao tác vận hành trên desktop/tablet. */
export default function CaptainLayout({ children }: { children: ReactNode }) {
  return <div className="captain-shell">{children}</div>
}
