import { Children, Fragment, isValidElement } from 'react'
import type { ChangeEvent, ReactElement, ReactNode } from 'react'
import { Listbox, ListboxButton, ListboxOption, ListboxOptions } from '@headlessui/react'
import FieldShell from './FieldShell'
import type { FieldShellProps } from './FieldShell'
import Icon from './Icon'

type ShellOptions = Pick<FieldShellProps, 'hideLabel' | 'fieldClassName'>

interface ParsedOption {
  value: string
  label: string
  disabled: boolean
}

const textOf = (node: ReactNode): string =>
  Children.toArray(node)
    .map((child) => (typeof child === 'string' || typeof child === 'number' ? String(child) : isValidElement(child) ? textOf((child.props as { children?: ReactNode }).children) : ''))
    .join('')

/** Đọc các `<option>` con (kể cả trong mảng / Fragment) thành danh sách lựa chọn. */
function parseOptions(children: ReactNode): ParsedOption[] {
  const out: ParsedOption[] = []
  for (const child of Children.toArray(children)) {
    if (!isValidElement(child)) continue
    const element = child as ReactElement<{ value?: string | number; disabled?: boolean; children?: ReactNode }>
    if (element.type === Fragment) {
      out.push(...parseOptions(element.props.children))
    } else if (element.type === 'option') {
      const label = textOf(element.props.children)
      out.push({ value: String(element.props.value ?? label), label, disabled: Boolean(element.props.disabled) })
    }
  }
  return out
}

interface SelectFieldProps extends ShellOptions {
  label: string
  hint?: string
  error?: string
  required?: boolean
  value?: string | number
  onChange?: (event: ChangeEvent<HTMLSelectElement>) => void
  disabled?: boolean
  /** Ô thấp hơn, dùng trong thanh công cụ (vd. số dòng mỗi trang). */
  compact?: boolean
  children?: ReactNode
}

/**
 * Ô chọn của khu điều hành: cùng kiểu danh sách thả xuống với `SearchSelect` (Headless UI), thay cho `<select>` mặc
 * định của trình duyệt. Giữ cách dùng cũ: truyền các `<option>` làm con và nhận `onChange` có `event.target.value`.
 */
export default function SelectField({
  label, hint, error, required, hideLabel, fieldClassName, value, onChange, disabled, compact, children,
}: SelectFieldProps) {
  const options = parseOptions(children)
  const current = String(value ?? '')
  const selected = options.find((option) => option.value === current)
  return (
    <FieldShell label={label} hint={hint} error={error} required={required} hideLabel={hideLabel} fieldClassName={fieldClassName}>
      {(id) => (
        <Listbox
          value={current}
          disabled={disabled}
          onChange={(next: string) => onChange?.({ target: { value: next }, currentTarget: { value: next } } as ChangeEvent<HTMLSelectElement>)}
        >
          <ListboxButton
            id={id}
            aria-required={required || undefined}
            aria-invalid={Boolean(error)}
            className={`flex w-full items-center gap-2 rounded-xl border bg-white px-3 text-left text-sm text-deep-river transition-colors focus:border-teal-flow focus:outline-none focus:ring-2 focus:ring-teal-flow/15 data-[open]:border-teal-flow disabled:cursor-not-allowed disabled:bg-surface-container-low ${
              compact ? 'min-h-[36px] py-1' : 'min-h-[42px] py-2'
            } ${error ? 'border-coral-glow' : 'border-outline-variant/70'}`}
          >
            <span className={`min-w-0 flex-1 truncate ${current === '' ? 'text-on-surface-variant' : ''}`}>{selected?.label ?? ''}</span>
            <Icon name="expand_more" className="shrink-0 text-xl text-on-surface-variant" />
          </ListboxButton>
          <ListboxOptions
            anchor="bottom start"
            modal={false}
            className="z-[100] max-h-72 w-[var(--button-width)] min-w-[8rem] overflow-y-auto overscroll-contain rounded-2xl border border-outline-variant/30 bg-white p-1.5 shadow-xl [--anchor-gap:8px] [--anchor-padding:12px] focus:outline-none"
          >
            {options.map((option) => (
              <ListboxOption
                key={option.value}
                data-value={option.value}
                value={option.value}
                disabled={option.disabled}
                className="group flex min-h-[40px] cursor-pointer items-center gap-3 rounded-xl px-3 py-2 text-sm text-deep-river data-[focus]:bg-teal-flow/10 data-[disabled]:cursor-not-allowed data-[disabled]:opacity-40"
              >
                <span className={`min-w-0 flex-1 truncate ${option.value === '' ? 'text-on-surface-variant' : 'font-semibold'}`}>{option.label}</span>
                <Icon name="check" className="shrink-0 text-xl text-teal-flow opacity-0 group-data-[selected]:opacity-100" />
              </ListboxOption>
            ))}
          </ListboxOptions>
        </Listbox>
      )}
    </FieldShell>
  )
}
