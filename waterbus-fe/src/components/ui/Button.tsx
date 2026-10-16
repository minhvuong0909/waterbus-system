import type { AnchorHTMLAttributes, ButtonHTMLAttributes, ReactNode } from 'react'
import { Link } from 'react-router-dom'

type Variant = 'primary' | 'secondary' | 'dark' | 'text'
type Size = 'md' | 'lg'

const VARIANTS: Record<Variant, string> = {
  primary: 'bg-teal-flow text-on-primary hover:bg-secondary shadow-sm',
  secondary: 'border border-teal-flow text-teal-flow bg-transparent hover:bg-sand-light',
  dark: 'bg-deep-river text-on-primary hover:bg-primary-container shadow-sm',
  text: 'text-teal-flow hover:bg-sand-light',
}

const SIZES: Record<Size, string> = {
  md: 'px-6 py-2.5 text-body-md',
  lg: 'px-8 py-3.5 text-body-lg',
}

interface CommonProps {
  variant?: Variant
  size?: Size
  className?: string
  children: ReactNode
}

type ButtonProps = CommonProps & ButtonHTMLAttributes<HTMLButtonElement> & { to?: undefined }
type LinkButtonProps = CommonProps &
  Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'href'> & { to: string }

function classes({ variant = 'primary', size = 'md', className = '' }: CommonProps) {
  return `inline-flex items-center justify-center gap-space-sm rounded-full font-headline-sm font-semibold transition-all disabled:opacity-50 disabled:cursor-not-allowed ${VARIANTS[variant]} ${SIZES[size]} ${className}`.trim()
}

/** Renders a react-router `<Link>` when `to` is given, otherwise a `<button>`. */
export default function Button(props: ButtonProps | LinkButtonProps) {
  if (props.to !== undefined) {
    const { to, variant, size, className, children, ...rest } = props
    return (
      <Link to={to} className={classes({ variant, size, className, children })} {...rest}>
        {children}
      </Link>
    )
  }
  const { variant, size, className, children, type = 'button', ...rest } = props
  return (
    <button type={type} className={classes({ variant, size, className, children })} {...rest}>
      {children}
    </button>
  )
}
