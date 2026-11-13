import { useState } from 'react'
import { Combobox, ComboboxButton, ComboboxInput, ComboboxOption, ComboboxOptions } from '@headlessui/react'
import FieldShell from './FieldShell'
import { t } from '../../i18n'

export interface SearchOption {
  value: string
  label: string
  description?: string
  badge?: string
  disabled?: boolean
}

const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLocaleLowerCase('vi-VN')

/** Ô chọn có tìm kiếm, quản lý focus/bàn phím và vị trí popup bằng Headless UI. */
export default function SearchSelect({ label, value, onChange, options, placeholder = 'Search and select…', disabled = false, hint }: {
  label: string
  value: string
  onChange: (value: string) => void
  options: SearchOption[]
  placeholder?: string
  disabled?: boolean
  hint?: string
}) {
  const [query, setQuery] = useState('')
  const selected = options.find((option) => option.value === value) ?? null
  const filtered = options.filter((option) => normalize(`${option.label} ${option.description ?? ''} ${option.badge ?? ''}`).includes(normalize(query)))
  return (
    <FieldShell label={label} hint={hint}>
      {(id) => (
        <Combobox value={selected} by="value" onChange={(option: SearchOption | null) => { onChange(option?.value ?? ''); setQuery('') }} onClose={() => setQuery('')} disabled={disabled} immediate>
          <div className="relative max-w-3xl">
            <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="pointer-events-none absolute left-3.5 top-3.5 h-5 w-5 text-on-surface-variant"><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 4 4" /></svg>
            <ComboboxInput id={id} displayValue={(option: SearchOption | null) => option ? `${option.label}${option.badge ? ` · ${option.badge}` : ''}` : ''}
              onChange={(event) => setQuery(event.target.value)} placeholder={t(placeholder)} autoComplete="off"
              className="min-h-[48px] w-full truncate rounded-xl border border-outline-variant/60 bg-white py-3 pl-11 pr-12 text-sm text-deep-river shadow-sm transition-colors placeholder:text-on-surface-variant focus:border-teal-flow focus:ring-2 focus:ring-teal-flow/15 disabled:bg-mist disabled:opacity-60" />
            <ComboboxButton aria-label={t('Show options')} className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-xl text-on-surface-variant hover:bg-mist">
              <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-5 w-5"><path d="m6 9 6 6 6-6" /></svg>
            </ComboboxButton>
            {selected?.description && <p className="mt-2 text-xs text-on-surface-variant">{selected.description}</p>}
          </div>
          <ComboboxOptions anchor="bottom start" modal={false} className="z-[100] max-h-80 w-[var(--input-width)] overflow-y-auto overscroll-contain rounded-2xl border border-outline-variant/40 bg-white p-1.5 shadow-xl [--anchor-gap:8px] [--anchor-padding:12px] focus:outline-none">
            {filtered.length === 0 && <div role="status" className="px-4 py-8 text-center text-sm text-on-surface-variant">{t('No matching options.')}</div>}
            {filtered.map((option) => (
              <ComboboxOption key={option.value} value={option} disabled={option.disabled} className="group flex min-h-[56px] cursor-pointer items-center gap-3 rounded-xl px-3 py-2.5 text-deep-river data-[focus]:bg-teal-flow/10 data-[disabled]:cursor-not-allowed data-[disabled]:opacity-40">
                <span className="min-w-0 flex-1"><span className="block truncate text-sm font-semibold">{option.label}</span>{option.description && <span className="mt-1 block truncate text-xs text-on-surface-variant">{option.description}</span>}</span>
                {option.badge && <span className="shrink-0 rounded-lg bg-mist px-2 py-1 font-mono text-xs font-semibold">{option.badge}</span>}
                <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="h-5 w-5 shrink-0 text-teal-flow opacity-0 group-data-[selected]:opacity-100"><path d="m5 12 4 4L19 6" /></svg>
              </ComboboxOption>
            ))}
          </ComboboxOptions>
        </Combobox>
      )}
    </FieldShell>
  )
}
