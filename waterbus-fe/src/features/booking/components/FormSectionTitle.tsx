import { Icon } from '../../../components/ui'

export default function FormSectionTitle({
  icon,
  title,
  subtitle,
}: {
  icon: string
  title: string
  subtitle: string
}) {
  return (
    <div className="mb-space-md flex items-center gap-space-sm">
      <div className="flex h-10 w-10 items-center justify-center rounded-full bg-sand-light text-teal-flow">
        <Icon name={icon} className="text-2xl" />
      </div>
      <div>
        <h2 className="font-display text-headline-sm text-deep-river">{title}</h2>
        <p className="text-xs text-on-surface-variant">{subtitle}</p>
      </div>
    </div>
  )
}
