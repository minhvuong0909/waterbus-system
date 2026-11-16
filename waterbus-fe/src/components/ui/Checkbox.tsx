import type { InputHTMLAttributes } from 'react'
import { CHECKBOX_CLASS } from './styles'

/** Ô đánh dấu dùng chung; đặt trong `<label>` hoặc truyền `aria-label`. */
export default function Checkbox({ className = '', ...input }: Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>) {
  return <input type="checkbox" className={`${CHECKBOX_CLASS} ${className}`.trim()} {...input} />
}
