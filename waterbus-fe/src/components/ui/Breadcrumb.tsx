import { Link } from 'react-router-dom'
import Icon from './Icon'
import { t } from '../../i18n'

export interface BreadcrumbItem {
  label: string
  to?: string
}

interface BreadcrumbProps {
  items: BreadcrumbItem[]
  className?: string
}

/** The last item is the current page and is rendered as plain text. */
export default function Breadcrumb({ items, className = '' }: BreadcrumbProps) {
  return (
    <nav
      aria-label={t('Breadcrumb')}
      className={`flex items-center gap-2 text-xs font-medium text-on-surface-variant ${className}`.trim()}
    >
      {items.map((item, i) => {
        const last = i === items.length - 1
        return (
          <span key={item.label} className="flex items-center gap-2">
            {item.to && !last ? (
              <Link to={item.to} className="transition-colors hover:text-deep-river">
                {t(item.label)}
              </Link>
            ) : (
              <span
                className="font-semibold text-deep-river"
                aria-current={last ? 'page' : undefined}
              >
                {t(item.label)}
              </span>
            )}
            {!last && <Icon name="chevron_right" className="text-[14px] text-outline" />}
          </span>
        )
      })}
    </nav>
  )
}
