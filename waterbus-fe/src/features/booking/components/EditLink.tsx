import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'

export default function EditLink({
  to,
  icon = 'edit',
  children,
}: {
  to: string
  icon?: string
  children: string
}) {
  return (
    <Link
      to={to}
      className="flex items-center gap-1 whitespace-nowrap text-xs font-semibold text-teal-flow hover:underline"
    >
      <Icon name={icon} className="text-sm" />
      {children}
    </Link>
  )
}
