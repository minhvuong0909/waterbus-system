import { Icon } from '../../../components/ui'

export default function SectionCard({
  icon,
  title,
  aside,
  children,
}: {
  icon: string
  title: string
  aside?: string
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-card">
      <div className="mb-space-md flex items-center justify-between">
        <div className="flex items-center gap-space-sm">
          <div className="flex h-9 w-9 items-center justify-center rounded-full bg-secondary-container text-teal-flow">
            <Icon name={icon} className="text-xl" />
          </div>
          <h2 className="font-display text-headline-md text-deep-river">{title}</h2>
        </div>
        {aside && <span className="text-body-sm text-on-surface-variant">{aside}</span>}
      </div>
      {children}
    </section>
  )
}
