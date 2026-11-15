import type { ReactNode } from 'react'
import SectionHeader from './SectionHeader'
import { SURFACE_CLASS } from './styles'

/** Khung trắng gom một phần của trang, có tiêu đề, mô tả và nút tuỳ chọn. */
export default function Panel({
  title,
  description,
  actions,
  children,
  className = '',
  id,
}: {
  title?: string
  description?: ReactNode
  actions?: ReactNode
  children: ReactNode
  className?: string
  id?: string
}) {
  return (
    <section id={id} className={`${SURFACE_CLASS} p-4 sm:p-5 ${className}`.trim()}>
      {title && <SectionHeader title={title} description={description} actions={actions} />}
      {children}
    </section>
  )
}
