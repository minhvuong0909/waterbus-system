import { describe, expect, it } from 'vitest'
import subset from './fonts/icon-subset.txt?raw'

/**
 * Font biểu tượng chỉ chứa các glyph đã cắt sẵn (xem fonts/README.md). Biểu tượng không có trong font sẽ hiện
 * nguyên tên chữ (vd "MANAGE_ACCOUNTS") thay vì hình. Test này chặn việc dùng một tên biểu tượng chưa có trong font.
 */
const sources = import.meta.glob(['/src/**/*.ts', '/src/**/*.tsx', '!/src/**/*.test.*'], { query: '?raw', import: 'default', eager: true }) as Record<
  string,
  string
>

const quoted = (text: string) => [...text.matchAll(/'([a-z0-9_]+)'/g)].map((m) => m[1])

function iconNamesIn(source: string): string[] {
  const names: string[] = []
  for (const tag of source.matchAll(/<Icon\b([^>]*)>/g)) {
    const attrs = tag[1]
    const literal = attrs.match(/\bname="([a-z0-9_]+)"/)
    if (literal) names.push(literal[1])
    const expression = attrs.match(/\bname=\{([^}]*)\}/)
    if (expression) names.push(...quoted(expression[1]))
  }
  for (const m of source.matchAll(/\bicon="([a-z0-9_]+)"/g)) names.push(m[1])
  for (const m of source.matchAll(/\bicon=\{([^}]*)\}/g)) names.push(...quoted(m[1]))
  for (const m of source.matchAll(/\bicon: ['"]([a-z0-9_]+)['"]/g)) names.push(m[1])
  for (const m of source.matchAll(/material-symbols-outlined[^>]*>([a-z0-9_]+)</g)) names.push(m[1])
  return names
}

describe('icon font subset', () => {
  const allowed = new Set(subset.split(/\r?\n/).filter(Boolean))

  it('contains every icon name used by the app', () => {
    const missing = Object.entries(sources).flatMap(([file, source]) =>
      iconNamesIn(source)
        .filter((name) => !allowed.has(name))
        .map((name) => `${name} (${file.replace('/src/', '')})`),
    )
    expect(missing, 'Thêm biểu tượng vào font (fonts/README.md) rồi vào icon-subset.txt, hoặc dùng biểu tượng đã có').toEqual([])
  })

  it('actually scans the source files', () => {
    expect(Object.keys(sources).length).toBeGreaterThan(50)
    expect(Object.values(sources).flatMap(iconNamesIn).length).toBeGreaterThan(50)
  })
})
