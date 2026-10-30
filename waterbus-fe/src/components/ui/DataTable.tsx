import type { ReactNode } from 'react'
import { t } from '../../i18n'

export interface Column<T> {
  header: string
  render: (row: T) => ReactNode
  className?: string
}

/** Dense table for the admin lists. */
export default function DataTable<T>({
  columns,
  rows,
  rowKey,
  empty = 'Nothing here yet.',
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  empty?: string
}) {
  return (
    <div className="overflow-x-auto rounded-2xl border border-outline-variant/30 bg-white shadow-card">
      <table className="w-full min-w-[40rem] text-left text-sm">
        <thead className="border-b border-outline-variant/30 bg-mist text-2xs uppercase tracking-wider text-on-surface-variant">
          <tr>
            {columns.map((c) => (
              <th
                key={c.header}
                scope="col"
                className={`whitespace-nowrap px-4 py-3 font-semibold ${c.className ?? ''}`.trim()}
              >
                {t(c.header)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-outline-variant/30">
          {rows.length === 0 ? (
            <tr>
              <td
                colSpan={columns.length}
                className="px-4 py-8 text-center text-on-surface-variant"
              >
                {t(empty)}
              </td>
            </tr>
          ) : (
            rows.map((row) => (
              <tr
                key={rowKey(row)}
                className="transition-colors hover:bg-teal-flow/[0.035] focus-within:bg-teal-flow/[0.035]"
              >
                {columns.map((c) => (
                  <td
                    key={c.header}
                    className={`px-4 py-3 text-deep-river first:whitespace-nowrap ${
                      c.className ?? ''
                    }`.trim()}
                  >
                    {c.render(row)}
                  </td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )
}
