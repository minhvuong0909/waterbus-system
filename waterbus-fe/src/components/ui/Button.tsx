import type { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from 'react'
import { Link } from 'react-router-dom'
import Icon from './Icon'

type Variant = 'primary' | 'secondary' | 'neutral' | 'danger' | 'dark' | 'text'
type Size = 'sm' | 'md' | 'lg'

const VARIANTS: Record<Variant, string> = {
  primary: 'bg-teal-flow text-on-primary hover:bg-secondary shadow-sm',
  secondary: 'border border-teal-flow text-teal-flow bg-transparent hover:bg-sand-light',
  neutral: 'border border-outline-variant bg-white text-deep-river hover:bg-surface-container-low',
  danger: 'bg-coral-glow text-white hover:opacity-90',
  dark: 'bg-deep-river text-on-primary hover:bg-primary-container shadow-sm',
  text: 'text-teal-flow hover:bg-sand-light',
}

const SIZES: Record<Size, string> = {
  sm: 'px-4 py-2 text-sm',
  md: 'px-6 py-2.5 text-body-md',
  lg: 'px-8 py-3.5 text-body-lg',
}

interface CommonProps {
  variant?: Variant
  size?: Size
  /** Material Symbols name shown before the label. */
  icon?: string
  className?: string
  children: ReactNode
}

type ButtonProps = CommonProps & ButtonHTMLAttributes<HTMLButtonElement> & { to?: undefined }
type LinkButtonProps = CommonProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> & { to: string }

function classes({ variant = 'primary', size = 'md', className = '' }: Omit<CommonProps, 'children'>) {
  return `inline-flex min-h-[44px] items-center justify-center gap-2 whitespace-nowrap rounded-full font-semibold transition-[color,background-color,border-color,box-shadow,transform] duration-200 ease-out enabled:active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed ${VARIANTS[variant]} ${SIZES[size]} ${className}`.trim()
}

/** The one button of the app. Renders a react-router `<Link>` when `to` is given, otherwise a `<button>`. */
export default function Button(props: ButtonProps | LinkButtonProps) {
  if (props.to !== undefined) {
    const { to, variant, size, icon, className, children, ...rest } = props
    return (
      <Link to={to} className={classes({ variant, size, className })} {...rest}>
        {icon && <Icon name={icon} className="text-lg" />}
        {children}
      </Link>
    )
  }
  const { variant, size, icon, className, children, type = 'button', ...rest } = props
  return (
    <button type={type} className={classes({ variant, size, className })} {...rest}>
      {icon && <Icon name={icon} className="text-lg" />}
      {children}
    </button>
  )
}
