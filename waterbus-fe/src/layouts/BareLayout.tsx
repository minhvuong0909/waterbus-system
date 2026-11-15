import type { ReactNode } from 'react'

/** Không thêm gì quanh trang: trang tự lo toàn bộ màn hình (đăng nhập, đăng ký, xác minh email). */
export default function BareLayout({ children }: { children: ReactNode }) {
  return <>{children}</>
}
