import { useState } from 'react'
import type { ReactNode } from 'react'
import { t } from '../../i18n'
import Button from './Button'

export interface Column<T> {
  header: string
  render: (row: T) => ReactNode
  className?: string
}

/**
 * Dense table for the admin lists. Give `pageSize` to show a long list a page at a time, with Previous / Next
 * below the table; without it every row is shown.
 */
export default function DataTable<T>({
  columns,
  rows,
  rowKey,
  empty = 'Nothing here yet.',
  pageSize,
}: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  empty?: string
  pageSize?: number
}) {
  const [requestedPage, setRequestedPage] = useState(1)
  const totalPages = pageSize ? Math.max(1, Math.ceil(rows.length / pageSize)) : 1
  // A filter or a refresh can leave fewer rows than the page we were on: fall back to the last page that exists.
  const page = Math.min(requestedPage, totalPages)
  const shown = pageSize ? rows.slice((page - 1) * pageSize, page * pageSize) : rows

  return (
    <div>
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
          {shown.length === 0 ? (
            <tr>
              <td
                colSpan={columns.length}
                className="px-4 py-8 text-center text-on-surface-variant"
              >
                {t(empty)}
              </td>
            </tr>
          ) : (
            shown.map((row) => (
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
      {totalPages > 1 && (
        <nav aria-label={t('Pages')} className="mt-3 flex items-center justify-between gap-3">
          <Button variant="neutral" size="sm" icon="chevron_left" onClick={() => setRequestedPage(page - 1)} disabled={page <= 1}>
            {t('Previous')}
          </Button>
          <span className="text-sm text-on-surface-variant">
            {t('Page {page} of {total}', { page, total: totalPages })} · {rows.length}
          </span>
          <Button variant="neutral" size="sm" icon="chevron_right" onClick={() => setRequestedPage(page + 1)} disabled={page >= totalPages}>
            {t('Next')}
          </Button>
        </nav>
      )}
    </div>
  )
}
